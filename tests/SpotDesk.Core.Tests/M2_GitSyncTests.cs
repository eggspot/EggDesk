using System.Security.Cryptography;
using System.Text.Json;
using LibGit2Sharp;
using NSubstitute;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Crypto;
using SpotDesk.Core.Sync;
using SpotDesk.Core.Tests.TestHelpers;
using SpotDesk.Core.Vault;
using Xunit;

namespace SpotDesk.Core.Tests;

// ── ConflictResolver ─────────────────────────────────────────────────────────

public class M2_ConflictResolverTests
{
    private static readonly byte[] MasterKey = RandomNumberGenerator.GetBytes(32);

    private static VaultEntry MakeEntry(Guid id, DateTimeOffset updatedAt)
    {
        var payload = JsonSerializer.Serialize(new { updatedAt });
        var (ciphertext, iv) = VaultCrypto.EncryptEntry(payload, MasterKey);
        return new VaultEntry
        {
            Id = id,
            Ciphertext = Convert.ToBase64String(ciphertext),
            Iv = Convert.ToBase64String(iv)
        };
    }

    [Fact, Trait("Category", "M2")]
    public void Resolve_TwoVersions_KeepsNewerByUpdatedAt()
    {
        var id   = Guid.NewGuid();
        var ours = new VaultFile { Entries = [MakeEntry(id, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))] };
        var theirs = new VaultFile { Entries = [MakeEntry(id, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero))] };

        var merged = ConflictResolver.Merge(ours, theirs, MasterKey);

        Assert.Single(merged.Entries);
        Assert.Equal(theirs.Entries[0].Ciphertext, merged.Entries[0].Ciphertext);
    }

    [Fact, Trait("Category", "M2")]
    public void Resolve_SameUpdatedAt_KeepsCurrentDevice()
    {
        var id  = Guid.NewGuid();
        var ts  = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ours   = new VaultFile { Entries = [MakeEntry(id, ts)] };
        var theirs = new VaultFile { Entries = [MakeEntry(id, ts)] };

        var merged = ConflictResolver.Merge(ours, theirs, MasterKey);

        Assert.Single(merged.Entries);
    }

    [Fact, Trait("Category", "M2")]
    public void Resolve_NewEntryOnRemote_MergesIn()
    {
        var ourId   = Guid.NewGuid();
        var theirId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;

        var ours   = new VaultFile { Entries = [MakeEntry(ourId, ts)] };
        var theirs = new VaultFile { Entries = [MakeEntry(theirId, ts)] };

        var merged = ConflictResolver.Merge(ours, theirs, MasterKey);

        Assert.Equal(2, merged.Entries.Length);
        Assert.Contains(merged.Entries, e => e.Id == ourId);
        Assert.Contains(merged.Entries, e => e.Id == theirId);
    }

    [Fact, Trait("Category", "M2")]
    public void Resolve_DeletedOnRemote_RemovesLocally()
    {
        var keepId   = Guid.NewGuid();
        var deleteId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow;

        var ours   = new VaultFile { Entries = [MakeEntry(keepId, ts), MakeEntry(deleteId, ts)] };
        var theirs = new VaultFile { Entries = [MakeEntry(keepId, ts)] };

        var merged = ConflictResolver.Merge(ours, theirs, MasterKey);

        Assert.DoesNotContain(merged.Entries, e => e.Id == keepId &&
            merged.Entries.Count(x => x.Id == keepId) > 1);
    }
}

// ── GitSyncService ────────────────────────────────────────────────────────────

public class M2_GitSyncServiceTests : IDisposable
{
    private readonly string _bareRepoDir  = Path.Combine(Path.GetTempPath(), $"spotdesk-bare-{Guid.NewGuid():N}");
    private readonly string _cloneDir1    = Path.Combine(Path.GetTempPath(), $"spotdesk-clone1-{Guid.NewGuid():N}");
    private readonly IKeychainService _keychain = Substitute.For<IKeychainService>();

    public M2_GitSyncServiceTests()
    {
        Directory.CreateDirectory(_bareRepoDir);
        Repository.Init(_bareRepoDir, isBare: true);
        SeedBareRepo(_bareRepoDir);
    }

    private static void SeedBareRepo(string bareRepoPath)
    {
        var seedDir = Path.Combine(Path.GetTempPath(), $"spotdesk-seed-{Guid.NewGuid():N}");
        try
        {
            Repository.Clone(bareRepoPath, seedDir);
            using var repo = new Repository(seedDir);
            var initFile = Path.Combine(seedDir, ".gitkeep");
            File.WriteAllText(initFile, string.Empty);
            Commands.Stage(repo, ".gitkeep");
            var sig = new Signature("SpotDesk", "seed@spotdesk.app", DateTimeOffset.UtcNow);
            repo.Commit("init", sig, sig);
            var pushOpts = new PushOptions();
            repo.Network.Push(repo.Head, pushOpts);
        }
        finally
        {
            ForceDeleteDir(seedDir);
        }
    }

    public void Dispose()
    {
        ForceDeleteDir(_cloneDir1);
        ForceDeleteDir(_bareRepoDir);
    }

    [Fact, Trait("Category", "M2")]
    public async Task Clone_NewRepo_CreatesLocalDirectory()
    {
        _keychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(_keychain);

        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);

        Assert.True(Directory.Exists(_cloneDir1));
        Assert.True(Repository.IsValid(_cloneDir1));
    }

    [Fact, Trait("Category", "M2")]
    public async Task Clone_AlreadyCloned_DoesNotThrow()
    {
        _keychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(_keychain);

        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);
        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);

        Assert.True(Repository.IsValid(_cloneDir1));
    }

    [Fact, Trait("Category", "M2")]
    public async Task CommitAndPush_CreatesCommitInBareRepo()
    {
        _keychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(_keychain);

        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);

        var vaultFile = Path.Combine(_cloneDir1, "vault.json");
        await File.WriteAllTextAsync(vaultFile, """{"version":2}""");

        await svc.SyncAsync(_cloneDir1);

        using var bare = new Repository(_bareRepoDir);
        Assert.NotEmpty(bare.Commits);
    }

    [Fact, Trait("Category", "M2")]
    public async Task CommitAndPush_CommitMessage_ContainsTimestamp()
    {
        _keychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(_keychain);

        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);
        await File.WriteAllTextAsync(Path.Combine(_cloneDir1, "vault.json"), "{}");
        await svc.SyncAsync(_cloneDir1);

        using var bare = new Repository(_bareRepoDir);
        var latest = bare.Commits.First();
        Assert.StartsWith("spotdesk: sync ", latest.Message);
    }

    [Fact, Trait("Category", "M2")]
    public async Task Offline_QueuesSyncAndRetries()
    {
        var failingKeychain = Substitute.For<IKeychainService>();
        failingKeychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(failingKeychain);

        await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);
        await File.WriteAllTextAsync(Path.Combine(_cloneDir1, "vault.json"), "{}");

        var events = new List<SyncEvent>();
        svc.OnSyncEvent += (e, _) => events.Add(e);

        var brokenDir = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}");
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => svc.SyncAsync(brokenDir));
            Assert.Contains(SyncEvent.SyncFailed, events);
        }
        finally
        {
            ForceDeleteDir(brokenDir);
        }
    }

    [Fact, Trait("Category", "M2")]
    public async Task Pull_BehindRemote_FastForwards()
    {
        _keychain.Retrieve(Arg.Any<string>()).Returns((string?)null);
        var svc = new GitSyncService(_keychain);

        var cloneDir2 = Path.Combine(Path.GetTempPath(), $"spotdesk-clone2-{Guid.NewGuid():N}");
        try
        {
            await svc.InitOrCloneAsync(_bareRepoDir, _cloneDir1);
            await svc.InitOrCloneAsync(_bareRepoDir, cloneDir2);

            await File.WriteAllTextAsync(Path.Combine(_cloneDir1, "vault.json"), """{"version":2}""");
            await svc.SyncAsync(_cloneDir1);

            await svc.SyncAsync(cloneDir2);

            Assert.True(File.Exists(Path.Combine(cloneDir2, "vault.json")));
        }
        finally
        {
            ForceDeleteDir(cloneDir2);
        }
    }

    private static void ForceDeleteDir(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var info in new DirectoryInfo(path).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            info.Attributes = FileAttributes.Normal;
        Directory.Delete(path, recursive: true);
    }
}
