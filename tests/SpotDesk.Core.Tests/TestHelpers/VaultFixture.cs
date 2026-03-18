using NSubstitute;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Crypto;
using SpotDesk.Core.Vault;

namespace SpotDesk.Core.Tests.TestHelpers;

/// <summary>
/// Provides a fresh in-memory vault + mock keychain for every test class.
/// </summary>
public sealed class VaultFixture : IDisposable
{
    public string VaultPath { get; } = Path.Combine(
        Path.GetTempPath(), $"spotdesk-test-{Guid.NewGuid():N}", "vault.json");

    public IKeychainService Keychain { get; } = Substitute.For<IKeychainService>();
    public IDeviceIdService DeviceId { get; } = Substitute.For<IDeviceIdService>();
    public IOAuthService OAuthService { get; } = Substitute.For<IOAuthService>();
    public IKeyDerivationService FastKdf { get; } = FastKeyDerivationService.Create();
    public SessionLockService Lock { get; } = new SessionLockService();

    public VaultFixture()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(VaultPath)!);
        DeviceId.GetDeviceId().Returns("test-device-abc123");
    }

    public VaultService CreateVaultService() =>
        new(Keychain, DeviceId, FastKdf, OAuthService, Lock);

    public void Dispose()
    {
        Lock.Dispose();
        var dir = Path.GetDirectoryName(VaultPath)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
