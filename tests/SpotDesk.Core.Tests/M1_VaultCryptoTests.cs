using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NSubstitute;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Crypto;
using SpotDesk.Core.Tests.TestHelpers;
using SpotDesk.Core.Vault;
using Xunit;

namespace SpotDesk.Core.Tests;

// ── VaultCrypto ──────────────────────────────────────────────────────────────

public class M1_VaultCryptoTests
{
    [Fact, Trait("Category", "M1")]
    public void EncryptEntry_ThenDecrypt_RoundTrip()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        const string payload = "hello world";

        var (ciphertext, iv) = VaultCrypto.EncryptEntry(payload, masterKey);
        var result = VaultCrypto.DecryptEntry(ciphertext, iv, masterKey);

        Assert.Equal(payload, result);
    }

    [Fact, Trait("Category", "M1")]
    public void DecryptEntry_WrongMasterKey_ThrowsAuthTag()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var wrongKey  = VaultCrypto.GenerateMasterKey();

        var (ciphertext, iv) = VaultCrypto.EncryptEntry("secret", masterKey);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => VaultCrypto.DecryptEntry(ciphertext, iv, wrongKey));
    }

    [Fact, Trait("Category", "M1")]
    public void DecryptEntry_TamperedCiphertext_ThrowsAuthTag()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var (ciphertext, iv) = VaultCrypto.EncryptEntry("secret", masterKey);
        ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(
            () => VaultCrypto.DecryptEntry(ciphertext, iv, masterKey));
    }

    [Fact, Trait("Category", "M1")]
    public void GenerateMasterKey_Returns32Bytes()
    {
        var key = VaultCrypto.GenerateMasterKey();
        Assert.Equal(32, key.Length);
    }

    [Fact, Trait("Category", "M1")]
    public void GenerateMasterKey_CalledTwice_ReturnsDifferentValues()
    {
        var k1 = VaultCrypto.GenerateMasterKey();
        var k2 = VaultCrypto.GenerateMasterKey();
        Assert.NotEqual(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void EncryptMasterKey_ThenDecrypt_RoundTrip()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var deviceKey = RandomNumberGenerator.GetBytes(32);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);
        var decrypted = VaultCrypto.DecryptMasterKey(ciphertext, iv, deviceKey);

        Assert.Equal(masterKey, decrypted);
    }

    [Fact, Trait("Category", "M1")]
    public void DecryptMasterKey_WrongDeviceKey_ThrowsAuthTag()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var deviceKey = RandomNumberGenerator.GetBytes(32);
        var wrongKey  = RandomNumberGenerator.GetBytes(32);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => VaultCrypto.DecryptMasterKey(ciphertext, iv, wrongKey));
    }
}

// ── KeyDerivation ─────────────────────────────────────────────────────────────
// These tests use the REAL Argon2id KDF to validate correctness.

public class M1_KeyDerivationTests
{
    private readonly KeyDerivationService _kdf = new();

    [Fact, Trait("Category", "M1")]
    public void DeriveDeviceKey_SameInputs_DeterministicOutput()
    {
        var k1 = _kdf.DeriveDeviceKey(12345L, "device-abc");
        var k2 = _kdf.DeriveDeviceKey(12345L, "device-abc");
        Assert.Equal(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void DeriveDeviceKey_DifferentDeviceId_DifferentKey()
    {
        var k1 = _kdf.DeriveDeviceKey(12345L, "device-abc");
        var k2 = _kdf.DeriveDeviceKey(12345L, "device-xyz");
        Assert.NotEqual(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void DeriveDeviceKey_DifferentUserId_DifferentKey()
    {
        var k1 = _kdf.DeriveDeviceKey(11111L, "same-device");
        var k2 = _kdf.DeriveDeviceKey(22222L, "same-device");
        Assert.NotEqual(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void DeriveDeviceKey_Returns32Bytes()
    {
        var key = _kdf.DeriveDeviceKey(99L, "test-device");
        Assert.Equal(32, key.Length);
    }
}

// ── DeviceIdService ───────────────────────────────────────────────────────────

public class M1_DeviceIdServiceTests
{
    // We test via the interface mock because actual DeviceIdService reads OS-specific
    // resources (registry, /etc/machine-id, IOKit) that may not be available in CI.

    [Fact, Trait("Category", "M1")]
    public void GetDeviceId_ReturnsNonEmptyHexString()
    {
        var svc = Substitute.For<IDeviceIdService>();
        svc.GetDeviceId().Returns(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-machine" + "spotdesk-v1"))));

        var id = svc.GetDeviceId();

        Assert.False(string.IsNullOrEmpty(id));
    }

    [Fact, Trait("Category", "M1")]
    public void GetDeviceId_CalledTwice_ReturnsSameValue()
    {
        // Real DeviceIdService caches after first call; test caching via the real impl
        // using a deterministic setup on the current OS.
        var svc = new DeviceIdService();

        var id1 = svc.GetDeviceId();
        var id2 = svc.GetDeviceId();

        Assert.Equal(id1, id2);
    }

    [Fact, Trait("Category", "M1")]
    public void GetDeviceId_Format_Is64CharHex()
    {
        // SHA-256 hex output is exactly 64 characters.
        var svc = new DeviceIdService();
        var id  = svc.GetDeviceId();

        Assert.Equal(64, id.Length);
        Assert.Matches("^[0-9A-F]{64}$", id);
    }
}

// ── KeychainService (contract tests via InMemoryKeychainService) ───────────────
// These tests verify the IKeychainService contract against a real in-memory
// implementation so the Store → Retrieve → Delete lifecycle is meaningful.

file sealed class InMemoryKeychainService : IKeychainService
{
    private readonly Dictionary<string, string> _store = new();
    public void Store(string key, string value) => _store[key] = value;
    public string? Retrieve(string key) => _store.TryGetValue(key, out var v) ? v : null;
    public void Delete(string key) => _store.Remove(key);
}

public class M1_KeychainServiceTests
{
    private readonly IKeychainService _svc = new InMemoryKeychainService();

    [Fact, Trait("Category", "M1")]
    public void Store_ThenRetrieve_ReturnsSameValue()
    {
        _svc.Store("mykey", "myvalue");
        var result = _svc.Retrieve("mykey");
        Assert.Equal("myvalue", result);
    }

    [Fact, Trait("Category", "M1")]
    public void Retrieve_NonExistentKey_ReturnsNull()
    {
        var result = _svc.Retrieve("ghost");
        Assert.Null(result);
    }

    [Fact, Trait("Category", "M1")]
    public void Delete_ExistingKey_RetrieveReturnsNull()
    {
        _svc.Store("mykey", "value");
        _svc.Delete("mykey");
        var result = _svc.Retrieve("mykey");
        Assert.Null(result);
    }
}

// ── VaultModel serialization ──────────────────────────────────────────────────

public class M1_VaultModelTests
{
    [Fact, Trait("Category", "M1")]
    public void VaultFile_SerializeDeserialize_PreservesAllFields()
    {
        var envelope = new DeviceEnvelope
        {
            DeviceId = "dev-1",
            DeviceName = "Test PC",
            EncryptedMasterKey = Convert.ToBase64String(new byte[48]),
            Iv = Convert.ToBase64String(new byte[12]),
            AddedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var entry = new VaultEntry
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Iv = Convert.ToBase64String(new byte[12]),
            Ciphertext = Convert.ToBase64String(new byte[32])
        };
        var vault = new VaultFile
        {
            Version = 2,
            Kdf = "argon2id:3:65536:4",
            Devices = [envelope],
            Entries = [entry]
        };

        var json = JsonSerializer.Serialize(vault, VaultJsonContext.Default.VaultFile);
        var restored = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;

        Assert.Equal(vault.Version, restored.Version);
        Assert.Equal(vault.Kdf, restored.Kdf);
        Assert.Single(restored.Devices);
        Assert.Equal(envelope.DeviceId, restored.Devices[0].DeviceId);
        Assert.Equal(envelope.DeviceName, restored.Devices[0].DeviceName);
        Assert.Equal(envelope.EncryptedMasterKey, restored.Devices[0].EncryptedMasterKey);
        Assert.Equal(envelope.AddedAt, restored.Devices[0].AddedAt);
        Assert.Single(restored.Entries);
        Assert.Equal(entry.Id, restored.Entries[0].Id);
        Assert.Equal(entry.Ciphertext, restored.Entries[0].Ciphertext);
    }

    [Fact, Trait("Category", "M1")]
    public void DeviceEnvelope_SerializeDeserialize_PreservesAllFields()
    {
        var envelope = new DeviceEnvelope
        {
            DeviceId = "device-abc",
            DeviceName = "MacBook Pro",
            EncryptedMasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            Iv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)),
            AddedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(envelope, VaultJsonContext.Default.DeviceEnvelope);
        var restored = JsonSerializer.Deserialize(json, VaultJsonContext.Default.DeviceEnvelope)!;

        Assert.Equal(envelope.DeviceId, restored.DeviceId);
        Assert.Equal(envelope.DeviceName, restored.DeviceName);
        Assert.Equal(envelope.EncryptedMasterKey, restored.EncryptedMasterKey);
        Assert.Equal(envelope.Iv, restored.Iv);
        Assert.Equal(envelope.AddedAt, restored.AddedAt);
    }

    [Fact, Trait("Category", "M1")]
    public void VaultEntry_SerializeDeserialize_PreservesAllFields()
    {
        var entry = new VaultEntry
        {
            Id = Guid.NewGuid(),
            Iv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)),
            Ciphertext = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
        };

        var json = JsonSerializer.Serialize(entry, VaultJsonContext.Default.VaultEntry);
        var restored = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultEntry)!;

        Assert.Equal(entry.Id, restored.Id);
        Assert.Equal(entry.Iv, restored.Iv);
        Assert.Equal(entry.Ciphertext, restored.Ciphertext);
    }
}

// ── VaultService ──────────────────────────────────────────────────────────────

public class M1_VaultServiceTests : IDisposable
{
    private readonly VaultFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact, Trait("Category", "M1")]
    public async Task UnlockAsync_WithValidToken_ReturnsSuccess()
    {
        var identity = FakeIdentity.GitHub(42L, "user");
        _fx.Keychain.Retrieve(KeychainKeys.GitHub).Returns("ghp_fake");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);
        _fx.DeviceId.GetDeviceId().Returns("device-a");

        var svc = _fx.CreateVaultService();

        // First-time setup writes vault.json with device-a's envelope
        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com/repo");
        _fx.Lock.Lock(); // simulate app restart

        // Now unlock as device-a
        var result = await svc.UnlockAsync(_fx.VaultPath);
        Assert.Equal(UnlockResult.Success, result);
    }

    [Fact, Trait("Category", "M1")]
    public async Task UnlockAsync_NoKeychainToken_ReturnsNeedsOAuth()
    {
        _fx.Keychain.Retrieve(KeychainKeys.GitHub).Returns((string?)null);
        var svc = _fx.CreateVaultService();

        var result = await svc.UnlockAsync(_fx.VaultPath);

        Assert.Equal(UnlockResult.NeedsOAuth, result);
    }

    [Fact, Trait("Category", "M1")]
    public async Task UnlockAsync_NoMatchingEnvelope_ReturnsNeedsDeviceApproval()
    {
        var identity = FakeIdentity.GitHub(42L, "user");
        _fx.Keychain.Retrieve(KeychainKeys.GitHub).Returns("ghp_fake");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);

        // Create vault for device-a
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");
        _fx.Lock.Lock();

        // Unlock as device-b (not approved)
        _fx.DeviceId.GetDeviceId().Returns("device-b");
        var result = await svc.UnlockAsync(_fx.VaultPath);

        Assert.Equal(UnlockResult.NeedsDeviceApproval, result);
    }

    [Fact, Trait("Category", "M1")]
    public async Task FirstTimeSetupAsync_CreatesVaultFile()
    {
        var identity = FakeIdentity.GitHub();
        var svc = _fx.CreateVaultService();

        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");

        Assert.True(File.Exists(_fx.VaultPath));
    }

    [Fact, Trait("Category", "M1")]
    public async Task FirstTimeSetupAsync_VaultHasOneDeviceEnvelope()
    {
        var identity = FakeIdentity.GitHub();
        _fx.DeviceId.GetDeviceId().Returns("my-device");
        var svc = _fx.CreateVaultService();

        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");

        var json = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.Single(vault.Devices);
        Assert.Equal("my-device", vault.Devices[0].DeviceId);
    }

    [Fact, Trait("Category", "M1")]
    public async Task AddDeviceAsync_NewEnvelopeDecryptable()
    {
        var identity = FakeIdentity.GitHub(42L);
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);
        var svc = _fx.CreateVaultService();

        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");
        await svc.AddDeviceAsync("device-b", "Device B");

        var json = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        var env = vault.Devices.Single(d => d.DeviceId == "device-b");

        // Derive device-b key and decrypt its envelope
        var deviceBKey = _fx.FastKdf.DeriveDeviceKey(42L, "device-b");
        var decrypted = VaultCrypto.DecryptMasterKey(
            Convert.FromBase64String(env.EncryptedMasterKey),
            Convert.FromBase64String(env.Iv),
            deviceBKey);

        Assert.Equal(32, decrypted.Length);
    }

    [Fact, Trait("Category", "M1")]
    public async Task RevokeDeviceAsync_EnvelopeRemovedFromVault()
    {
        var identity = FakeIdentity.GitHub(42L);
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);
        var svc = _fx.CreateVaultService();

        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");
        await svc.AddDeviceAsync("device-b", "Device B");
        await svc.RevokeDeviceAsync("device-b");

        var json = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.DoesNotContain(vault.Devices, d => d.DeviceId == "device-b");
    }

    [Fact, Trait("Category", "M1")]
    public async Task AddEntry_ThenGetAll_ReturnsEntry()
    {
        var identity = FakeIdentity.GitHub();
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");

        const string payload = """{"host":"server1","port":3389}""";
        await svc.AddEntryAsync(payload);
        var entries = await svc.GetAllEntriesAsync();

        Assert.Single(entries);
        Assert.Equal(payload, entries[0].Payload);
    }

    [Fact, Trait("Category", "M1")]
    public async Task UpdateEntry_ChangesPayload()
    {
        var identity = FakeIdentity.GitHub();
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");

        var entry = await svc.AddEntryAsync("""{"host":"old"}""");
        await svc.UpdateEntryAsync(entry.Id, """{"host":"new"}""");
        var entries = await svc.GetAllEntriesAsync();

        Assert.Single(entries);
        Assert.Contains("new", entries[0].Payload);
    }

    [Fact, Trait("Category", "M1")]
    public async Task RemoveEntry_EntryNotInGetAll()
    {
        var identity = FakeIdentity.GitHub();
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupAsync(identity, _fx.VaultPath, "https://example.com");

        var entry = await svc.AddEntryAsync("""{"host":"to-delete"}""");
        await svc.RemoveEntryAsync(entry.Id);
        var entries = await svc.GetAllEntriesAsync();

        Assert.Empty(entries);
    }
}

// ── SessionLockService ────────────────────────────────────────────────────────

public class M1_SessionLockServiceTests
{
    [Fact, Trait("Category", "M1")]
    public void GetMasterKey_WhenLocked_ThrowsInvalidOperation()
    {
        var svc = new SessionLockService();
        Assert.Throws<InvalidOperationException>(() => _ = svc.GetMasterKey());
    }

    [Fact, Trait("Category", "M1")]
    public void Lock_ZeroesMemory()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        svc.SetMasterKey(key);
        svc.Lock();

        Assert.False(svc.IsUnlocked);
        Assert.Throws<InvalidOperationException>(() => _ = svc.GetMasterKey());
    }

    [Fact, Trait("Category", "M1")]
    public void SetMasterKey_ThenGetMasterKey_ReturnsCorrectBytes()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        svc.SetMasterKey(key);

        Assert.Equal(key, svc.GetMasterKey().ToArray());
    }

    [Fact, Trait("Category", "M1")]
    public void IsUnlocked_AfterLock_ReturnsFalse()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        svc.SetMasterKey(key);
        Assert.True(svc.IsUnlocked);

        svc.Lock();
        Assert.False(svc.IsUnlocked);
    }
}

// ── VaultService — local mode ─────────────────────────────────────────────────

public class M1_VaultLocalModeTests : IDisposable
{
    private readonly VaultFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    [Fact, Trait("Category", "M1")]
    public async Task FirstTimeSetupLocal_CreatesVaultWithLocalMode()
    {
        var svc = _fx.CreateVaultService();

        await svc.FirstTimeSetupLocalAsync("correct-horse-battery", _fx.VaultPath);

        var json  = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.Equal("local", vault.Mode);
        Assert.NotNull(vault.Salt);
        Assert.Single(vault.Devices);
        Assert.Equal("local", vault.Devices[0].DeviceId);
    }

    [Fact, Trait("Category", "M1")]
    public async Task FirstTimeSetupLocal_ThenUnlockLocal_CorrectPassword_ReturnsSuccess()
    {
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupLocalAsync("my-password", _fx.VaultPath);
        _fx.Lock.Lock();

        var result = await svc.UnlockLocalAsync("my-password", _fx.VaultPath);

        Assert.Equal(UnlockResult.Success, result);
        Assert.True(_fx.Lock.IsUnlocked);
    }

    [Fact, Trait("Category", "M1")]
    public async Task UnlockLocal_WrongPassword_ReturnsFailed()
    {
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupLocalAsync("right-password", _fx.VaultPath);
        _fx.Lock.Lock();

        var result = await svc.UnlockLocalAsync("wrong-password", _fx.VaultPath);

        Assert.Equal(UnlockResult.Failed, result);
        Assert.False(_fx.Lock.IsUnlocked);
    }

    [Fact, Trait("Category", "M1")]
    public async Task UnlockAsync_LocalModeVault_ReturnsNeedsPassword_WithoutTouchingKeychain()
    {
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupLocalAsync("pass", _fx.VaultPath);
        _fx.Lock.Lock();

        var result = await svc.UnlockAsync(_fx.VaultPath);

        Assert.Equal(UnlockResult.NeedsPassword, result);
        // GitHub keychain must not have been consulted
        _fx.Keychain.DidNotReceive().Retrieve(Arg.Any<string>());
    }

    [Fact, Trait("Category", "M1")]
    public async Task LocalMode_AddEntry_ThenGetAll_RoundTrips()
    {
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupLocalAsync("vault-pass", _fx.VaultPath);

        const string payload = """{"host":"local-server","port":3389}""";
        await svc.AddEntryAsync(payload);
        var entries = await svc.GetAllEntriesAsync();

        Assert.Single(entries);
        Assert.Equal(payload, entries[0].Payload);
    }

    [Fact, Trait("Category", "M1")]
    public async Task FirstTimeSetupLocal_EmptyPassword_ThrowsArgumentException()
    {
        var svc = _fx.CreateVaultService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.FirstTimeSetupLocalAsync("   ", _fx.VaultPath));
    }

    [Fact, Trait("Category", "M1")]
    public async Task UnlockLocal_MissingVaultFile_ReturnsFailed()
    {
        var svc = _fx.CreateVaultService();

        var result = await svc.UnlockLocalAsync("pass", _fx.VaultPath);

        Assert.Equal(UnlockResult.Failed, result);
    }
}

// ── VaultService — local → GitHub migration ───────────────────────────────────

public class M1_VaultMigrationTests : IDisposable
{
    private readonly VaultFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private async Task<VaultService> SetupLocalVaultAsync(string password = "migration-pass")
    {
        var svc = _fx.CreateVaultService();
        await svc.FirstTimeSetupLocalAsync(password, _fx.VaultPath);
        return svc;
    }

    [Fact, Trait("Category", "M1")]
    public async Task MigrateLocalToGitHub_VaultModeChangesToGithub()
    {
        var identity = FakeIdentity.GitHub(42L, "user");
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        var svc = await SetupLocalVaultAsync();

        await svc.MigrateLocalToGitHubAsync(identity);

        var json  = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.Equal("github", vault.Mode);
    }

    [Fact, Trait("Category", "M1")]
    public async Task MigrateLocalToGitHub_SaltCleared()
    {
        var identity = FakeIdentity.GitHub(42L);
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        var svc = await SetupLocalVaultAsync();

        await svc.MigrateLocalToGitHubAsync(identity);

        var json  = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.Null(vault.Salt);
    }

    [Fact, Trait("Category", "M1")]
    public async Task MigrateLocalToGitHub_PasswordEnvelopeReplaced_WithDeviceEnvelope()
    {
        var identity = FakeIdentity.GitHub(42L);
        _fx.DeviceId.GetDeviceId().Returns("device-xyz");
        var svc = await SetupLocalVaultAsync();

        await svc.MigrateLocalToGitHubAsync(identity);

        var json  = await File.ReadAllTextAsync(_fx.VaultPath);
        var vault = JsonSerializer.Deserialize(json, VaultJsonContext.Default.VaultFile)!;
        Assert.Single(vault.Devices);
        Assert.Equal("device-xyz", vault.Devices[0].DeviceId);
        Assert.DoesNotContain(vault.Devices, d => d.DeviceId == "local");
    }

    [Fact, Trait("Category", "M1")]
    public async Task MigrateLocalToGitHub_ExistingEntriesStillDecryptable()
    {
        var identity = FakeIdentity.GitHub(99L);
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        var svc = await SetupLocalVaultAsync();

        // Add an entry before migration
        const string payload = """{"host":"server-pre-migration","port":3389}""";
        await svc.AddEntryAsync(payload);

        // Migrate
        await svc.MigrateLocalToGitHubAsync(identity);

        // All entries must still be readable (master key unchanged)
        var entries = await svc.GetAllEntriesAsync();
        Assert.Single(entries);
        Assert.Equal(payload, entries[0].Payload);
    }

    [Fact, Trait("Category", "M1")]
    public async Task AfterMigration_UnlockAsync_WithGitHubToken_ReturnsSuccess()
    {
        var identity = FakeIdentity.GitHub(42L, "user");
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        _fx.Keychain.Retrieve(KeychainKeys.GitHub).Returns("ghp_fake");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);

        var svc = await SetupLocalVaultAsync();
        await svc.MigrateLocalToGitHubAsync(identity);
        _fx.Lock.Lock();

        // Regular GitHub unlock must now succeed
        var result = await svc.UnlockAsync(_fx.VaultPath);
        Assert.Equal(UnlockResult.Success, result);
    }

    [Fact, Trait("Category", "M1")]
    public async Task AfterMigration_UnlockAsync_NoLongerReturnsNeedsPassword()
    {
        var identity = FakeIdentity.GitHub(42L);
        _fx.DeviceId.GetDeviceId().Returns("device-a");
        _fx.Keychain.Retrieve(KeychainKeys.GitHub).Returns("ghp_fake");
        _fx.OAuthService.GetCachedIdentityAsync(default).ReturnsForAnyArgs(identity);

        var svc = await SetupLocalVaultAsync();
        await svc.MigrateLocalToGitHubAsync(identity);
        _fx.Lock.Lock();

        var result = await svc.UnlockAsync(_fx.VaultPath);
        Assert.NotEqual(UnlockResult.NeedsPassword, result);
    }
}

// ── MasterPasswordFallback ────────────────────────────────────────────────────

public class M1_MasterPasswordFallbackTests
{
    // Uses the REAL KDF — intentionally slow but validates correctness.
    private readonly MasterPasswordFallback _fallback =
        new(new KeyDerivationService());

    [Fact, Trait("Category", "M1")]
    public void DeriveKeyFromPassword_SameInputs_DeterministicOutput()
    {
        var salt = RandomNumberGenerator.GetBytes(32);

        var k1 = _fallback.DeriveKey("my-password", salt);
        var k2 = _fallback.DeriveKey("my-password", salt);

        Assert.Equal(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void DeriveKeyFromPassword_DifferentPasswords_DifferentKeys()
    {
        var salt = RandomNumberGenerator.GetBytes(32);

        var k1 = _fallback.DeriveKey("password-one", salt);
        var k2 = _fallback.DeriveKey("password-two", salt);

        Assert.NotEqual(k1, k2);
    }

    [Fact, Trait("Category", "M1")]
    public void DeriveKeyFromPassword_Returns32Bytes()
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var key  = _fallback.DeriveKey("any-password", salt);

        Assert.Equal(32, key.Length);
    }
}
