using System.Collections.Concurrent;
using RemoteViewing.Vnc;
using SpotDesk.Core.Models;

namespace SpotDesk.Protocols.Vnc;

public class VncSessionManager
{
    private readonly ConcurrentDictionary<Guid, VncClient> _sessions = new();

    public async Task<VncClient> ConnectAsync(ConnectionEntry connection, CredentialEntry credential, CancellationToken ct = default)
    {
        var client = new VncClient();

        var options = new VncClientConnectOptions
        {
            Password = credential.Password?.ToCharArray()
        };

        await Task.Run(() => client.Connect(connection.Host, connection.Port, options), ct);

        _sessions[connection.Id] = client;
        return client;
    }

    public void Close(Guid connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var client))
            client.Close();
    }
}
