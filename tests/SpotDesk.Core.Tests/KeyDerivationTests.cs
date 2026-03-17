using SpotDesk.Core.Crypto;
using Xunit;

namespace SpotDesk.Core.Tests;

public class KeyDerivationTests
{
    private readonly KeyDerivationService _kdf = new();

    [Fact]
    public void DeriveDeviceKey_SameInputs_SameKey()
    {
        var key1 = _kdf.DeriveDeviceKey(12345L, "device-abc");
        var key2 = _kdf.DeriveDeviceKey(12345L, "device-abc");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveDeviceKey_DifferentDeviceId_DifferentKey()
    {
        var key1 = _kdf.DeriveDeviceKey(12345L, "device-abc");
        var key2 = _kdf.DeriveDeviceKey(12345L, "device-xyz");

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void DeriveDeviceKey_DifferentUserId_DifferentKey()
    {
        var key1 = _kdf.DeriveDeviceKey(11111L, "same-device");
        var key2 = _kdf.DeriveDeviceKey(22222L, "same-device");

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void DeriveDeviceKey_Returns32Bytes()
    {
        var key = _kdf.DeriveDeviceKey(99L, "test-device");
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void DeriveFromPassword_SameInputs_SameKey()
    {
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        var k1 = _kdf.DeriveFromPassword("my-password", salt);
        var k2 = _kdf.DeriveFromPassword("my-password", salt);

        Assert.Equal(k1, k2);
    }

    [Fact]
    public void DeriveFromPassword_DifferentPassword_DifferentKey()
    {
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        var k1 = _kdf.DeriveFromPassword("password1", salt);
        var k2 = _kdf.DeriveFromPassword("password2", salt);

        Assert.NotEqual(k1, k2);
    }
}
