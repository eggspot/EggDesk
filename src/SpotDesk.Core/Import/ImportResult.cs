using SpotDesk.Core.Models;

namespace SpotDesk.Core.Import;

public record ImportResult
{
    public ConnectionEntry[] Connections { get; init; } = [];
    public CredentialEntry[] Credentials { get; init; } = [];
    public string[] Warnings { get; init; } = [];
    public string[] Errors { get; init; } = [];
    public bool HasErrors => Errors.Length > 0;
}
