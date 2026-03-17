using System.Security.Cryptography;
using System.Text;

namespace SpotDesk.Core.Crypto;

/// <summary>
/// All methods are static and NativeAOT-safe. Uses AES-256-GCM exclusively.
/// </summary>
public static class VaultCrypto
{
    private const int KeySize = 32;    // 256-bit
    private const int IvSize = 12;     // 96-bit nonce (GCM standard)
    private const int TagSize = 16;    // 128-bit authentication tag

    public static byte[] GenerateMasterKey() =>
        RandomNumberGenerator.GetBytes(KeySize);

    public static (byte[] Ciphertext, byte[] Iv) EncryptMasterKey(byte[] masterKey, byte[] deviceKey)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentNullException.ThrowIfNull(deviceKey);

        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var ciphertext = new byte[masterKey.Length + TagSize];

        using var aes = new AesGcm(deviceKey, TagSize);
        aes.Encrypt(iv, masterKey, ciphertext.AsSpan(0, masterKey.Length), ciphertext.AsSpan(masterKey.Length));

        return (ciphertext, iv);
    }

    public static byte[] DecryptMasterKey(byte[] ciphertext, byte[] iv, byte[] deviceKey)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(iv);
        ArgumentNullException.ThrowIfNull(deviceKey);

        var plaintext = new byte[ciphertext.Length - TagSize];

        using var aes = new AesGcm(deviceKey, TagSize);
        // throws AuthenticationTagMismatchException on wrong key or tampered data
        aes.Decrypt(iv, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length), plaintext);

        return plaintext;
    }

    public static (byte[] Ciphertext, byte[] Iv) EncryptEntry(string payload, byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(masterKey);

        var plainBytes = Encoding.UTF8.GetBytes(payload);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var ciphertext = new byte[plainBytes.Length + TagSize];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Encrypt(iv, plainBytes, ciphertext.AsSpan(0, plainBytes.Length), ciphertext.AsSpan(plainBytes.Length));

        return (ciphertext, iv);
    }

    public static string DecryptEntry(byte[] ciphertext, byte[] iv, byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(iv);
        ArgumentNullException.ThrowIfNull(masterKey);

        var plainBytes = new byte[ciphertext.Length - TagSize];

        using var aes = new AesGcm(masterKey, TagSize);
        aes.Decrypt(iv, ciphertext.AsSpan(0, plainBytes.Length), ciphertext.AsSpan(plainBytes.Length), plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
