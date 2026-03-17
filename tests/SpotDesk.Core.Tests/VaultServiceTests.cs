using NSubstitute;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Crypto;
using SpotDesk.Core.Vault;
using Xunit;

namespace SpotDesk.Core.Tests;

public class VaultServiceTests
{
    private readonly IKeychainService _keychain = Substitute.For<IKeychainService>();
    private readonly IDeviceIdService _deviceId = Substitute.For<IDeviceIdService>();
    private readonly IKeyDerivationService _kdf = new KeyDerivationService();
    private readonly IOAuthService _oauth = Substitute.For<IOAuthService>();
    private readonly SessionLockService _lock = new();

    private VaultService CreateSvc() =>
        new(_keychain, _deviceId, _kdf, _oauth, _lock);

    [Fact]
    public async Task UnlockAsync_NoKeychainToken_ReturnsNeedsOAuth()
    {
        _keychain.Retrieve(KeychainKeys.GitHub).Returns((string?)null);
        var svc = CreateSvc();

        var result = await svc.UnlockAsync("/tmp/vault.json");

        Assert.Equal(UnlockResult.NeedsOAuth, result);
    }

    [Fact]
    public async Task UnlockAsync_NoVaultFile_ReturnsNeedsOAuth()
    {
        _keychain.Retrieve(KeychainKeys.GitHub).Returns("fake-token");
        _oauth.GetCachedIdentityAsync().Returns(new GitHubIdentity(99L, "testuser", "fake-token"));
        var svc = CreateSvc();

        var result = await svc.UnlockAsync("/nonexistent/vault.json");

        Assert.Equal(UnlockResult.NeedsOAuth, result);
    }

    [Fact]
    public async Task AddAndGetEntry_RoundTrip_Succeeds()
    {
        // Set up vault with a master key directly
        var masterKey = VaultCrypto.GenerateMasterKey();
        _lock.SetMasterKey(masterKey);

        // Use temp vault path
        var vaultPath = Path.Combine(Path.GetTempPath(), $"test-vault-{Guid.NewGuid()}.json");
        try
        {
            // Bootstrap vault file
            _keychain.Retrieve(KeychainKeys.GitHub).Returns("fake-token");
            _deviceId.GetDeviceId().Returns("test-device-id");
            _oauth.GetCachedIdentityAsync().Returns(new GitHubIdentity(42L, "user", "fake-token"));

            var svc = CreateSvc();
            await svc.FirstTimeSetupAsync(
                new GitHubIdentity(42L, "user", "fake-token"),
                vaultPath, "https://github.com/user/vault");

            const string payload = """{"username":"admin","password":"s3cr3t"}""";
            await svc.AddEntryAsync(payload);

            var entries = await svc.GetAllEntriesAsync();
            Assert.Single(entries);
            Assert.Equal(payload, entries[0].Payload);
        }
        finally
        {
            if (File.Exists(vaultPath)) File.Delete(vaultPath);
        }
    }
}
