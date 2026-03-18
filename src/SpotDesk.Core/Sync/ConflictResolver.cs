using System.Text.Json;
using SpotDesk.Core.Vault;

namespace SpotDesk.Core.Sync;

/// <summary>
/// Resolves vault.json conflicts using last-write-wins by the updatedAt
/// field inside each decrypted payload.
/// </summary>
public static class ConflictResolver
{
    /// <summary>
    /// Merges two vault files. For each entry Id, the one with the newer
    /// updatedAt wins. Device envelopes are union-merged (no entries dropped).
    /// </summary>
    public static VaultFile Merge(VaultFile ours, VaultFile theirs, byte[] masterKey)
    {
        // Devices: union by deviceId, newest addedAt wins on conflict
        var mergedDevices = ours.Devices
            .Concat(theirs.Devices)
            .GroupBy(d => d.DeviceId)
            .Select(g => g.OrderByDescending(d => d.AddedAt).First())
            .ToArray();

        // Entries: decrypt both, keep newest by updatedAt
        var ourDecrypted = DecryptAll(ours.Entries, masterKey, "ours");
        var theirDecrypted = DecryptAll(theirs.Entries, masterKey, "theirs");

        var merged = ourDecrypted
            .Concat(theirDecrypted)
            .GroupBy(e => e.Id)
            .Select(g => g.OrderByDescending(e => e.UpdatedAt).First())
            .ToList();

        // Re-encrypt with the same ciphertexts from the winning side
        var winnerEntries = merged
            .Select(m => m.Source == "ours"
                ? ours.Entries.First(e => e.Id == m.Id)
                : theirs.Entries.First(e => e.Id == m.Id))
            .ToArray();

        return ours with { Devices = mergedDevices, Entries = winnerEntries };
    }

    private static List<DecryptedMeta> DecryptAll(VaultEntry[] entries, byte[] masterKey, string source)
    {
        var results = new List<DecryptedMeta>();
        foreach (var entry in entries)
        {
            try
            {
                var json = Crypto.VaultCrypto.DecryptEntry(
                    Convert.FromBase64String(entry.Ciphertext),
                    Convert.FromBase64String(entry.Iv),
                    masterKey);
                var doc = JsonDocument.Parse(json);
                var updatedAt = doc.RootElement.TryGetProperty("updatedAt", out var el)
                    ? el.GetDateTimeOffset()
                    : DateTimeOffset.MinValue;
                results.Add(new(entry.Id, updatedAt, source));
            }
            catch
            {
                // Cannot decrypt — skip (wrong key or corrupt)
            }
        }
        return results;
    }

    private record DecryptedMeta(Guid Id, DateTimeOffset UpdatedAt, string Source);
}
