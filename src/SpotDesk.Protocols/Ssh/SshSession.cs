using Renci.SshNet;
using SpotDesk.Core.Models;

namespace SpotDesk.Protocols.Ssh;

public interface ISshSession : IDisposable
{
    Guid Id { get; }
    SessionStatus Status { get; }
    bool IsConnected { get; }

    event Action<SessionStatus> StatusChanged;
    event Action<string> OutputReceived;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    void SendInput(string text);
    void Resize(int cols, int rows);
}

public class SshSession : ISshSession
{
    private readonly ConnectionEntry _connection;
    private readonly CredentialEntry _credential;
    private SshClient? _client;
    private ShellStream? _shell;

    public Guid Id { get; } = Guid.NewGuid();
    public SessionStatus Status { get; private set; } = SessionStatus.Idle;
    public bool IsConnected => _client?.IsConnected ?? false;

    public event Action<SessionStatus> StatusChanged = delegate { };
    public event Action<string> OutputReceived = delegate { };

    public SshSession(ConnectionEntry connection, CredentialEntry credential)
    {
        _connection = connection;
        _credential = credential;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        SetStatus(SessionStatus.Connecting);

        var authMethods = BuildAuthMethods();
        var connInfo = new ConnectionInfo(_connection.Host, _connection.Port, _credential.Username, [.. authMethods]);
        _client = new SshClient(connInfo);

        await Task.Run(() => _client.Connect(), ct);

        _shell = _client.CreateShellStream("xterm-256color", 220, 50, 0, 0, 4096);
        _shell.DataReceived += (_, e) => OutputReceived(System.Text.Encoding.UTF8.GetString(e.Data));

        SetStatus(SessionStatus.Connected);
    }

    public Task DisconnectAsync()
    {
        SetStatus(SessionStatus.Disconnecting);
        _shell?.Dispose();
        _client?.Disconnect();
        SetStatus(SessionStatus.Idle);
        return Task.CompletedTask;
    }

    public void SendInput(string text) =>
        _shell?.Write(text);

    public void Resize(int cols, int rows) =>
        _shell?.Write(System.Text.Encoding.UTF8.GetBytes($"\x1b[8;{rows};{cols}t"), 0, 0);

    private List<AuthenticationMethod> BuildAuthMethods()
    {
        var methods = new List<AuthenticationMethod>();

        if (_credential.SshKeyPath is not null && File.Exists(_credential.SshKeyPath))
        {
            var keyFile = _credential.SshKeyPassphrase is not null
                ? new PrivateKeyFile(_credential.SshKeyPath, _credential.SshKeyPassphrase)
                : new PrivateKeyFile(_credential.SshKeyPath);
            methods.Add(new PrivateKeyAuthenticationMethod(_credential.Username, keyFile));
        }

        // Try SSH agent ($SSH_AUTH_SOCK) on Linux/macOS
        var agentSocket = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        if (!string.IsNullOrEmpty(agentSocket) && File.Exists(agentSocket))
        {
            // SSH.NET agent integration — connects to Unix socket
            // methods.Add(new AgentAuthenticationMethod(...)); // TODO
        }

        if (_credential.Password is not null)
            methods.Add(new PasswordAuthenticationMethod(_credential.Username, _credential.Password));

        return methods;
    }

    private void SetStatus(SessionStatus s) { Status = s; StatusChanged(s); }

    public void Dispose()
    {
        _shell?.Dispose();
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
