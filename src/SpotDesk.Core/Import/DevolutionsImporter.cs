using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using SpotDesk.Core.Models;

namespace SpotDesk.Core.Import;

/// <summary>
/// Parses .rdm files exported from Devolutions Remote Desktop Manager.
/// Format: XML with &lt;DataEntries&gt; root and &lt;Connection&gt; children.
/// </summary>
public class DevolutionsImporter
{
    // Devolutions connection type codes
    private const int RdpType = 1;
    private const int SshType = 66;
    private const int VncType = 12;

    public ImportResult Import(Stream stream, string? rdmMasterKey = null)
    {
        var connections = new List<ConnectionEntry>();
        var credentials = new List<CredentialEntry>();
        var warnings = new List<string>();
        var errors = new List<string>();

        try
        {
            Stream xmlStream = rdmMasterKey is not null
                ? DecryptRdmStream(stream, rdmMasterKey)
                : stream;

            var doc = XDocument.Load(xmlStream);
            var entries = doc.Descendants("Connection");

            foreach (var entry in entries)
            {
                try
                {
                    var (conn, cred) = ParseConnection(entry, warnings);
                    if (conn is not null) connections.Add(conn);
                    if (cred is not null) credentials.Add(cred);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Skipped entry '{entry.Element("Name")?.Value}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse .rdm file: {ex.Message}");
        }

        return new ImportResult
        {
            Connections = [.. connections],
            Credentials = [.. credentials],
            Warnings = [.. warnings],
            Errors = [.. errors]
        };
    }

    private static (ConnectionEntry? Connection, CredentialEntry? Credential) ParseConnection(
        XElement entry, List<string> warnings)
    {
        var typeStr = entry.Element("ConnectionType")?.Value;
        if (!int.TryParse(typeStr, out var type)) return (null, null);

        var protocol = type switch
        {
            RdpType => Protocol.Rdp,
            SshType => Protocol.Ssh,
            VncType => Protocol.Vnc,
            _ => (Protocol?)null
        };

        if (protocol is null)
        {
            warnings.Add($"Unknown connection type {type}, skipping.");
            return (null, null);
        }

        var name = entry.Element("Name")?.Value ?? "Unnamed";
        var host = entry.Element("Host")?.Value ?? string.Empty;
        var portStr = entry.Element("Port")?.Value;
        var username = entry.Element("UserName")?.Value ?? string.Empty;
        var passwordRaw = entry.Element("Password")?.Value;
        var group = entry.Element("Group")?.Value;
        var tagsStr = entry.Element("Tags")?.Value;

        var port = portStr is not null && int.TryParse(portStr, out var p)
            ? p
            : ConnectionEntry.DefaultPortFor(protocol.Value);

        // RDM may base64-encode passwords
        var password = TryDecodeBase64(passwordRaw);

        var credId = Guid.NewGuid();
        CredentialEntry? cred = null;

        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
        {
            cred = new CredentialEntry
            {
                Id = credId,
                Name = $"{name} credential",
                Username = username,
                Password = password
            };
        }

        var conn = new ConnectionEntry
        {
            Name = name,
            Host = host,
            Port = port,
            Protocol = protocol.Value,
            CredentialId = cred?.Id,
            Tags = tagsStr?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []
        };

        return (conn, cred);
    }

    private static string? TryDecodeBase64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            // Sanity check: decoded text should be printable
            return decoded.All(c => !char.IsControl(c) || c == '\r' || c == '\n') ? decoded : value;
        }
        catch
        {
            return value;
        }
    }

    private static Stream DecryptRdmStream(Stream encrypted, string masterKey)
    {
        // RDM uses AES-CBC with a key derived from the master password
        // This is a stub — real implementation depends on RDM's specific scheme
        throw new NotImplementedException("Encrypted .rdm decryption not yet implemented.");
    }
}
