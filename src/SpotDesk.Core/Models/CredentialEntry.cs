namespace SpotDesk.Core.Models;

public record CredentialEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    /// <summary>Plaintext password — only exists in memory after vault decrypt. Never persisted as-is.</summary>
    public string? Password { get; set; }
    /// <summary>Path to SSH private key file on disk.</summary>
    public string? SshKeyPath { get; set; }
    /// <summary>Passphrase for the SSH key, if encrypted.</summary>
    public string? SshKeyPassphrase { get; set; }
    public CredentialType Type { get; set; } = CredentialType.UsernamePassword;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum CredentialType { UsernamePassword, SshKey, SshAgent }
