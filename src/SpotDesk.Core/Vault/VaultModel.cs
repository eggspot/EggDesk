using System.Text.Json.Serialization;

namespace SpotDesk.Core.Vault;

// All records use source-gen serialization for NativeAOT compatibility.
// JsonSerializerContext is in VaultJsonContext.cs

public record VaultFile
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 2;

    [JsonPropertyName("kdf")]
    public string Kdf { get; init; } = "argon2id:3:65536:4";

    /// <summary>
    /// "github" (default) — master key derived from GitHub userId + deviceId.
    /// "local"            — master key derived from a user password + salt (no Git sync required).
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "github";

    /// <summary>Only present when Mode == "local". Base64-encoded 32-byte random salt.</summary>
    [JsonPropertyName("salt")]
    public string? Salt { get; init; }

    [JsonPropertyName("devices")]
    public DeviceEnvelope[] Devices { get; init; } = [];

    [JsonPropertyName("entries")]
    public VaultEntry[] Entries { get; init; } = [];
}

public record DeviceEnvelope
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; init; } = string.Empty;

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>Base64 AES-256-GCM(deviceKey, masterKey)</summary>
    [JsonPropertyName("encryptedMasterKey")]
    public string EncryptedMasterKey { get; init; } = string.Empty;

    [JsonPropertyName("iv")]
    public string Iv { get; init; } = string.Empty;

    [JsonPropertyName("addedAt")]
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record VaultEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Base64 12-byte nonce, unique per entry.</summary>
    [JsonPropertyName("iv")]
    public string Iv { get; init; } = string.Empty;

    /// <summary>Base64 AES-256-GCM(masterKey, UTF8(payloadJson))</summary>
    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; init; } = string.Empty;
}

[JsonSerializable(typeof(VaultFile))]
[JsonSerializable(typeof(DeviceEnvelope))]
[JsonSerializable(typeof(VaultEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
public partial class VaultJsonContext : JsonSerializerContext;
