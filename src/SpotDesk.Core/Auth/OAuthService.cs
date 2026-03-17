using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SpotDesk.Core.Auth;

public record GitHubIdentity(long UserId, string Login, string AccessToken);
public record BitbucketIdentity(string UserId, string Username, string AccessToken);

public enum OAuthProvider { GitHub, Bitbucket }

public interface IOAuthService
{
    Task<GitHubIdentity> AuthenticateGitHubAsync(CancellationToken ct = default);
    Task<BitbucketIdentity> AuthenticateBitbucketAsync(CancellationToken ct = default);
    Task<GitHubIdentity> GetCachedIdentityAsync(CancellationToken ct = default);
    Task<bool> IsAuthenticatedAsync(OAuthProvider provider, CancellationToken ct = default);
    Task RevokeAsync(OAuthProvider provider);
}

public class OAuthService : IOAuthService
{
    private readonly IKeychainService _keychain;
    private readonly HttpClient _http;

    private GitHubIdentity? _githubCache;
    private DateTimeOffset _githubCacheExpiry;

    public OAuthService(IKeychainService keychain, HttpClient? http = null)
    {
        _keychain = keychain;
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SpotDesk/1.0");
    }

    public async Task<GitHubIdentity> AuthenticateGitHubAsync(CancellationToken ct = default)
    {
        var port        = GetFreePort();
        var redirectUri = $"http://localhost:{port}/callback";
        var (verifier, challenge) = GeneratePkce();
        var state       = GenerateState();

        var authUrl = "https://github.com/login/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(SpotDeskSecrets.GitHubClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString("read:user repo")}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256" +
            $"&response_type=code";

        var code = await RunBrowserFlowAsync(authUrl, port, state, ct);

        // GitHub PKCE token exchange — no client_secret required
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"]     = SpotDeskSecrets.GitHubClientId,
                ["code"]          = code,
                ["redirect_uri"]  = redirectUri,
                ["code_verifier"] = verifier,
            })
        };
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var tokenData = await resp.Content.ReadFromJsonAsync(GitHubTokenContext.Default.GitHubTokenResponse, ct)
            ?? throw new InvalidDataException("GitHub token response was null");

        _keychain.Store(KeychainKeys.GitHub, tokenData.AccessToken);
        return await FetchGitHubIdentityAsync(tokenData.AccessToken, ct);
    }

    public async Task<BitbucketIdentity> AuthenticateBitbucketAsync(CancellationToken ct = default)
    {
        var port        = GetFreePort();
        var redirectUri = $"http://localhost:{port}/callback";
        var (verifier, challenge) = GeneratePkce();
        var state       = GenerateState();

        var authUrl = "https://bitbucket.org/site/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(SpotDeskSecrets.BitbucketClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString("account")}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256" +
            $"&response_type=code";

        var code = await RunBrowserFlowAsync(authUrl, port, state, ct);

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{SpotDeskSecrets.BitbucketClientId}:{SpotDeskSecrets.BitbucketClientSecret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://bitbucket.org/site/oauth2/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "authorization_code",
                ["code"]          = code,
                ["redirect_uri"]  = redirectUri,
                ["code_verifier"] = verifier,
            })
        };
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var tokenData = await resp.Content.ReadFromJsonAsync(BitbucketTokenContext.Default.BitbucketTokenResponse, ct)
            ?? throw new InvalidDataException("Bitbucket token response was null");

        _keychain.Store(KeychainKeys.Bitbucket, tokenData.AccessToken);

        var user = await _http.GetFromJsonAsync<BitbucketUserResponse>(
            "https://api.bitbucket.org/2.0/user",
            BitbucketJsonContext.Default.BitbucketUserResponse, ct)
            ?? throw new InvalidDataException("Bitbucket user API returned null");

        return new BitbucketIdentity(user.AccountId, user.Username, tokenData.AccessToken);
    }

    public async Task<GitHubIdentity> GetCachedIdentityAsync(CancellationToken ct = default)
    {
        if (_githubCache is not null && DateTimeOffset.UtcNow < _githubCacheExpiry)
            return _githubCache;

        var token = _keychain.Retrieve(KeychainKeys.GitHub)
            ?? throw new InvalidOperationException("No GitHub token in keychain.");

        return await FetchGitHubIdentityAsync(token, ct);
    }

    public Task<bool> IsAuthenticatedAsync(OAuthProvider provider, CancellationToken ct = default)
    {
        var key = provider == OAuthProvider.GitHub ? KeychainKeys.GitHub : KeychainKeys.Bitbucket;
        return Task.FromResult(_keychain.Retrieve(key) is not null);
    }

    public Task RevokeAsync(OAuthProvider provider)
    {
        var key = provider == OAuthProvider.GitHub ? KeychainKeys.GitHub : KeychainKeys.Bitbucket;
        _keychain.Delete(key);
        if (provider == OAuthProvider.GitHub) _githubCache = null;
        return Task.CompletedTask;
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private async Task<GitHubIdentity> FetchGitHubIdentityAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        request.Headers.Authorization = new("token", token);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.GitHubUserResponse, ct)
            ?? throw new InvalidDataException("GitHub user API returned null");

        var identity = new GitHubIdentity(user.Id, user.Login, token);
        _githubCache       = identity;
        _githubCacheExpiry = DateTimeOffset.UtcNow.AddHours(24);
        return identity;
    }

    private static async Task<string> RunBrowserFlowAsync(
        string authUrl, int port, string expectedState, CancellationToken ct)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = authUrl,
            UseShellExecute = true
        });

        var prefix   = $"http://localhost:{port}/callback/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(5));

            var contextTask = listener.GetContextAsync();
            await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));

            if (!contextTask.IsCompletedSuccessfully)
                throw new OperationCanceledException("OAuth timed out.");

            var context = await contextTask;
            var rawUrl  = context.Request.RawUrl ?? string.Empty;

            var html = "<html><body><h2>SpotDesk: Authentication complete. You can close this tab.</h2></body></html>"u8.ToArray();
            context.Response.ContentType      = "text/html";
            context.Response.ContentLength64  = html.Length;
            await context.Response.OutputStream.WriteAsync(html, CancellationToken.None);
            context.Response.Close();

            var query = ParseQuery(rawUrl);

            if (query.TryGetValue("error", out var oauthError))
                throw new InvalidOperationException($"OAuth denied: {oauthError}");

            if (!query.TryGetValue("state", out var returnedState) || returnedState != expectedState)
                throw new InvalidOperationException("OAuth state mismatch.");

            if (!query.TryGetValue("code", out var code))
                throw new InvalidOperationException("No code in OAuth callback.");

            return code;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Dictionary<string, string> ParseQuery(string rawUrl)
    {
        var result = new Dictionary<string, string>();
        var idx    = rawUrl.IndexOf('?');
        if (idx < 0) return result;
        foreach (var part in rawUrl[(idx + 1)..].Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            result[Uri.UnescapeDataString(part[..eq])] = Uri.UnescapeDataString(part[(eq + 1)..]);
        }
        return result;
    }

    private static (string verifier, string challenge) GeneratePkce()
    {
        var verifier  = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string GenerateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int GetFreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

// ── build-time secrets (injected via env vars in CI, user sets locally) ──────

file static class SpotDeskSecrets
{
    public static string GitHubClientId      => Environment.GetEnvironmentVariable("SPOTDESK_GITHUB_CLIENT_ID")       ?? "YOUR_GITHUB_CLIENT_ID";
    public static string BitbucketClientId   => Environment.GetEnvironmentVariable("SPOTDESK_BITBUCKET_CLIENT_ID")    ?? "YOUR_BITBUCKET_CLIENT_ID";
    public static string BitbucketClientSecret => Environment.GetEnvironmentVariable("SPOTDESK_BITBUCKET_CLIENT_SECRET") ?? "YOUR_BITBUCKET_CLIENT_SECRET";
}

// ── JSON models ───────────────────────────────────────────────────────────────

internal record GitHubUserResponse
{
    [JsonPropertyName("id")]    public long   Id    { get; init; }
    [JsonPropertyName("login")] public string Login { get; init; } = string.Empty;
}

internal record GitHubTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("token_type")]   public string TokenType   { get; init; } = string.Empty;
    [JsonPropertyName("scope")]        public string Scope        { get; init; } = string.Empty;
}

internal record BitbucketUserResponse
{
    [JsonPropertyName("account_id")] public string AccountId { get; init; } = string.Empty;
    [JsonPropertyName("username")]   public string Username  { get; init; } = string.Empty;
}

internal record BitbucketTokenResponse
{
    [JsonPropertyName("access_token")]  public string AccessToken  { get; init; } = string.Empty;
    [JsonPropertyName("token_type")]    public string TokenType    { get; init; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; init; } = string.Empty;
}

[JsonSerializable(typeof(GitHubUserResponse))]
internal partial class GitHubJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(GitHubTokenResponse))]
internal partial class GitHubTokenContext : JsonSerializerContext;

[JsonSerializable(typeof(BitbucketUserResponse))]
internal partial class BitbucketJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(BitbucketTokenResponse))]
internal partial class BitbucketTokenContext : JsonSerializerContext;
