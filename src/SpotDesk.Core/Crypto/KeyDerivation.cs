using System.Text;
using Konscious.Security.Cryptography;

namespace SpotDesk.Core.Crypto;

public interface IKeyDerivationService
{
    byte[] DeriveDeviceKey(long githubUserId, string deviceId);
    byte[] DeriveFromPassword(string password, byte[] salt);
}

public class KeyDerivationService : IKeyDerivationService
{
    // Domain separation salt — non-secret, just prevents cross-context key reuse
    private static readonly byte[] DeviceKeySalt = Encoding.UTF8.GetBytes("spotdesk-device-key-v1");

    private const int Iterations = 3;
    private const int MemoryKb = 65536; // 64 MB
    private const int Parallelism = 4;
    private const int KeyLength = 32;   // 256-bit

    /// <summary>
    /// Derives a deterministic device key from GitHub userId + deviceId.
    /// Same inputs always produce the same key — this is intentional.
    /// </summary>
    public byte[] DeriveDeviceKey(long githubUserId, string deviceId)
    {
        var input = Encoding.UTF8.GetBytes($"{githubUserId}:{deviceId}");
        return RunArgon2id(input, DeviceKeySalt);
    }

    /// <summary>
    /// Derives a master key from a user-entered password + random salt (fallback mode).
    /// Salt must be stored in vault.json and must be unique per vault.
    /// </summary>
    public byte[] DeriveFromPassword(string password, byte[] salt)
    {
        var input = Encoding.UTF8.GetBytes(password);
        return RunArgon2id(input, salt);
    }

    private static byte[] RunArgon2id(byte[] password, byte[] salt)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemoryKb,
            Iterations = Iterations
        };
        return argon2.GetBytes(KeyLength);
    }
}
