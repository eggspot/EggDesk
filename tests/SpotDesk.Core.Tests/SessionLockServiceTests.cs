using SpotDesk.Core.Vault;
using Xunit;

namespace SpotDesk.Core.Tests;

public class SessionLockServiceTests
{
    [Fact]
    public void InitialState_IsLocked()
    {
        var svc = new SessionLockService();
        Assert.False(svc.IsUnlocked);
    }

    [Fact]
    public void SetMasterKey_Then_GetMasterKey_ReturnsKey()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        svc.SetMasterKey(key);

        Assert.True(svc.IsUnlocked);
        Assert.Equal(key, svc.GetMasterKey().ToArray());
    }

    [Fact]
    public void GetMasterKey_WhenLocked_Throws()
    {
        var svc = new SessionLockService();
        Assert.Throws<InvalidOperationException>(() => _ = svc.GetMasterKey());
    }

    [Fact]
    public void Lock_AfterUnlock_IsLocked()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        svc.SetMasterKey(key);
        svc.Lock();

        Assert.False(svc.IsUnlocked);
        Assert.Throws<InvalidOperationException>(() => _ = svc.GetMasterKey());
    }

    [Fact]
    public void SetMasterKey_MustBe32Bytes_OtherwiseThrows()
    {
        var svc = new SessionLockService();
        Assert.Throws<ArgumentException>(() => svc.SetMasterKey(new byte[16]));
    }

    [Fact]
    public void Dispose_ClearsKey()
    {
        var svc = new SessionLockService();
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        svc.SetMasterKey(key);
        svc.Dispose();

        Assert.False(svc.IsUnlocked);
    }
}
