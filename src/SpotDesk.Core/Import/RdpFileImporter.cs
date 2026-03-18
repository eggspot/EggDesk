using SpotDesk.Core.Models;

namespace SpotDesk.Core.Import;

/// <summary>
/// Parses standard Windows .rdp files (key:type:value format).
/// </summary>
public class RdpFileImporter
{
    public ImportResult Import(Stream stream)
    {
        var connections = new List<ConnectionEntry>();
        var errors = new List<string>();

        try
        {
            using var reader = new StreamReader(stream);
            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split(':', 3);
                if (parts.Length >= 3)
                    props[parts[0].Trim()] = parts[2].Trim();
            }

            var fullAddress = props.GetValueOrDefault("full address") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fullAddress))
            {
                errors.Add("Missing required field: full address");
                return new ImportResult { Errors = [.. errors] };
            }
            var (host, port) = ParseAddress(fullAddress);

            var username = props.GetValueOrDefault("username") ?? string.Empty;
            var width = props.TryGetValue("desktopwidth", out var w) && int.TryParse(w, out var wi) ? wi : (int?)null;
            var height = props.TryGetValue("desktopheight", out var h) && int.TryParse(h, out var hi) ? hi : (int?)null;
            var bpp = props.TryGetValue("session bpp", out var b) && int.TryParse(b, out var bi) ? bi : 32;

            var conn = new ConnectionEntry
            {
                Name = host,
                Host = host,
                Port = port,
                Protocol = Protocol.Rdp,
                DesktopWidth = width,
                DesktopHeight = height,
                ColorDepth = bpp
            };

            connections.Add(conn);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse .rdp file: {ex.Message}");
        }

        return new ImportResult
        {
            Connections = [.. connections],
            Errors = [.. errors]
        };
    }

    private static (string Host, int Port) ParseAddress(string fullAddress)
    {
        var colonIdx = fullAddress.LastIndexOf(':');
        if (colonIdx > 0 && int.TryParse(fullAddress[(colonIdx + 1)..], out var port))
            return (fullAddress[..colonIdx], port);
        return (fullAddress, 3389);
    }
}
