using System.Security.Cryptography;
using SpotDesk.Core.Crypto;
using Xunit;

namespace SpotDesk.Core.Tests;

public class VaultCryptoTests
{
    [Fact]
    public void EncryptDecryptMasterKey_RoundTrip_Succeeds()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var deviceKey = RandomNumberGenerator.GetBytes(32);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);
        var decrypted = VaultCrypto.DecryptMasterKey(ciphertext, iv, deviceKey);

        Assert.Equal(masterKey, decrypted);
    }

    [Fact]
    public void DecryptMasterKey_WrongKey_ThrowsAuthTagMismatch()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var deviceKey = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => VaultCrypto.DecryptMasterKey(ciphertext, iv, wrongKey));
    }

    [Fact]
    public void DecryptMasterKey_TamperedCiphertext_Throws()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        var deviceKey = RandomNumberGenerator.GetBytes(32);

        var (ciphertext, iv) = VaultCrypto.EncryptMasterKey(masterKey, deviceKey);
        ciphertext[0] ^= 0xFF; // Tamper

        Assert.Throws<AuthenticationTagMismatchException>(
            () => VaultCrypto.DecryptMasterKey(ciphertext, iv, deviceKey));
    }

    [Fact]
    public void EncryptDecryptEntry_RoundTrip_Succeeds()
    {
        var masterKey = VaultCrypto.GenerateMasterKey();
        const string payload = """{"username":"admin","password":"secret123"}""";

        var (ciphertext, iv) = VaultCrypto.EncryptEntry(payload, masterKey);
        var decrypted = VaultCrypto.DecryptEntry(ciphertext, iv, masterKey);

        Assert.Equal(payload, decrypted);
    }

    [Fact]
    public void GenerateMasterKey_Returns32Bytes()
    {
        var key = VaultCrypto.GenerateMasterKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void GenerateMasterKey_EachCallUnique()
    {
        var k1 = VaultCrypto.GenerateMasterKey();
        var k2 = VaultCrypto.GenerateMasterKey();
        Assert.NotEqual(k1, k2);
    }
}
