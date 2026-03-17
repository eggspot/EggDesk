using System.Text.Json;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Crypto;

namespace SpotDesk.Core.Vault;

public enum UnlockResult { Success, NeedsOAuth, NeedsDeviceApproval, Failed }

public interface IVaultService
{
    Task<UnlockResult> UnlockAsync(string vaultPath, CancellationToken ct = default);
    Task FirstTimeSetupAsync(GitHubIdentity identity, string vaultPath, string repoUrl, CancellationToken ct = default);
    Task AddDeviceAsync(string newDeviceId, string newDeviceName, CancellationToken ct = default);
    Task RevokeDeviceAsync(string deviceId, CancellationToken ct = default);
    Task<VaultEntry> AddEntryAsync(string payloadJson, CancellationToken ct = default);
    Task<VaultEntry> UpdateEntryAsync(Guid id, string payloadJson, CancellationToken ct = default);
    Task RemoveEntryAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid Id, string Payload)>> GetAllEntriesAsync(CancellationToken ct = default);
}

public class VaultService : IVaultService
{
    private readonly IKeychainService _keychain;
    private readonly IDeviceIdService _deviceId;
    private readonly IKeyDerivationService _kdf;
    private readonly IOAuthService _oauth;
    private readonly ISessionLockService _lock;

    private VaultFile? _vault;
    private string? _vaultPath;

    public VaultService(
        IKeychainService keychain,
        IDeviceIdService deviceId,
        IKeyDerivationService kdf,
        IOAuthService oauth,
        ISessionLockService sessionLock)
    {
        _keychain = keychain;
        _deviceId = deviceId;
        _kdf = kdf;
        _oauth = oauth;
        _lock = sessionLock;
    }

    public async Task<UnlockResult> UnlockAsync(string vaultPath, CancellationToken ct = default)
    {
        _vaultPath = vaultPath;

        var token = _keychain.Retrieve(KeychainKeys.GitHub);
        if (token is null)
            return UnlockResult.NeedsOAuth;

        GitHubIdentity identity;
        try
        {
            identity = await _oauth.GetCachedIdentityAsync(ct);
        }
        catch
        {
            return UnlockResult.NeedsOAuth;
        }

        if (!File.Exists(vaultPath))
            return UnlockResult.NeedsOAuth; // triggers FirstTimeSetupAsync

        _vault = await LoadVaultAsync(vaultPath, ct);

        var deviceId = _deviceId.GetDeviceId();
        var envelope = _vault.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (envelope is null)
            return UnlockResult.NeedsDeviceApproval;

        try
        {
            var deviceKey = _kdf.DeriveDeviceKey(identity.UserId, deviceId);
            var encKey = Convert.FromBase64String(envelope.EncryptedMasterKey);
            var iv = Convert.FromBase64String(envelope.Iv);
            var masterKey = VaultCrypto.DecryptMasterKey(encKey, iv, deviceKey);
            _lock.SetMasterKey(masterKey);
            return UnlockResult.Success;
        }
        catch
        {
            return UnlockResult.Failed;
        }
    }

    public async Task FirstTimeSetupAsync(GitHubIdentity identity, string vaultPath, string repoUrl, CancellationToken ct = default)
    {
        _vaultPath = vaultPath;
        var deviceId = _deviceId.GetDeviceId();
        var deviceKey = _kdf.DeriveDeviceKey(identity.UserId, deviceId);

        var masterKey = VaultCrypto.GenerateMasterKey();
        _lock.SetMasterKey(masterKey);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);

        var envelope = new DeviceEnvelope
        {
            DeviceId = deviceId,
            DeviceName = Environment.MachineName,
            EncryptedMasterKey = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv)
        };

        _vault = new VaultFile { Devices = [envelope], Entries = [] };
        await SaveVaultAsync(ct);
    }

    public async Task AddDeviceAsync(string newDeviceId, string newDeviceName, CancellationToken ct = default)
    {
        EnsureUnlocked();
        var identity = await _oauth.GetCachedIdentityAsync(ct);

        var newDeviceKey = _kdf.DeriveDeviceKey(identity.UserId, newDeviceId);
        var masterKey = _lock.GetMasterKey().ToArray();
        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, newDeviceKey);

        var newEnvelope = new DeviceEnvelope
        {
            DeviceId = newDeviceId,
            DeviceName = newDeviceName,
            EncryptedMasterKey = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv)
        };

        _vault = _vault! with { Devices = [.. _vault.Devices, newEnvelope] };
        await SaveVaultAsync(ct);
    }

    public async Task RevokeDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        EnsureUnlocked();
        _vault = _vault! with { Devices = _vault.Devices.Where(d => d.DeviceId != deviceId).ToArray() };
        await SaveVaultAsync(ct);
    }

    public async Task<VaultEntry> AddEntryAsync(string payloadJson, CancellationToken ct = default)
    {
        EnsureUnlocked();
        var (ciphertext, iv) = VaultCrypto.EncryptEntry(payloadJson, _lock.GetMasterKey().ToArray());
        var entry = new VaultEntry
        {
            Id = Guid.NewGuid(),
            Ciphertext = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv)
        };
        _vault = _vault! with { Entries = [.. _vault.Entries, entry] };
        await SaveVaultAsync(ct);
        return entry;
    }

    public async Task<VaultEntry> UpdateEntryAsync(Guid id, string payloadJson, CancellationToken ct = default)
    {
        EnsureUnlocked();
        await RemoveEntryAsync(id, ct);
        var (ciphertext, iv) = VaultCrypto.EncryptEntry(payloadJson, _lock.GetMasterKey().ToArray());
        var entry = new VaultEntry
        {
            Id = id,
            Ciphertext = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv)
        };
        _vault = _vault! with { Entries = [.. _vault.Entries, entry] };
        await SaveVaultAsync(ct);
        return entry;
    }

    public async Task RemoveEntryAsync(Guid id, CancellationToken ct = default)
    {
        EnsureUnlocked();
        _vault = _vault! with { Entries = _vault.Entries.Where(e => e.Id != id).ToArray() };
        await SaveVaultAsync(ct);
    }

    public Task<IReadOnlyList<(Guid Id, string Payload)>> GetAllEntriesAsync(CancellationToken ct = default)
    {
        EnsureUnlocked();
        var masterKey = _lock.GetMasterKey().ToArray();
        var results = _vault!.Entries
            .Select(e => (e.Id, VaultCrypto.DecryptEntry(
                Convert.FromBase64String(e.Ciphertext),
                Convert.FromBase64String(e.Iv),
                masterKey)))
            .ToList();
        return Task.FromResult<IReadOnlyList<(Guid, string)>>(results);
    }

    private void EnsureUnlocked()
    {
        if (!_lock.IsUnlocked)
            throw new InvalidOperationException("Vault is locked.");
        if (_vault is null || _vaultPath is null)
            throw new InvalidOperationException("Vault not loaded.");
    }

    private static async Task<VaultFile> LoadVaultAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, VaultJsonContext.Default.VaultFile, ct)
               ?? throw new InvalidDataException("Invalid vault file.");
    }

    private async Task SaveVaultAsync(CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_vaultPath!);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(_vaultPath!);
        await JsonSerializer.SerializeAsync(stream, _vault!, VaultJsonContext.Default.VaultFile, ct);
    }
}
