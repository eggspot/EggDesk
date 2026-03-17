using System.Runtime.InteropServices;

namespace SpotDesk.Core.Vault;

public interface ISessionLockService
{
    bool IsUnlocked { get; }
    void SetMasterKey(byte[] key);
    ReadOnlySpan<byte> GetMasterKey();
    void Lock();
}

/// <summary>
/// Holds the master key in GC-pinned memory so the GC cannot move it.
/// Call Lock() or Dispose to zero and free it.
/// </summary>
public sealed class SessionLockService : ISessionLockService, IDisposable
{
    private GCHandle _handle;
    private byte[]? _key;

    public bool IsUnlocked => _key is not null;

    public void SetMasterKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32)
            throw new ArgumentException("Master key must be 32 bytes.", nameof(key));

        Lock(); // zero any existing key first

        _key = key;
        _handle = GCHandle.Alloc(_key, GCHandleType.Pinned);
    }

    public ReadOnlySpan<byte> GetMasterKey()
    {
        if (_key is null)
            throw new InvalidOperationException("Vault is locked. Call UnlockAsync first.");
        return _key.AsSpan();
    }

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
        if (_handle.IsAllocated)
            _handle.Free();
    }

    public void Dispose() => Lock();
}

// Reference System.Security.Cryptography for ZeroMemory
file static class CryptographicOperations
{
    public static void ZeroMemory(byte[] buffer) =>
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer);
}
