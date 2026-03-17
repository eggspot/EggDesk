using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SpotDesk.Core.Models;
using SpotDesk.Protocols.Ssh;

namespace SpotDesk.Protocols;

public interface ISessionManager
{
    IRdpSession GetOrCreateRdp(ConnectionEntry connection, CredentialEntry credential);
    ISshSession GetOrCreateSsh(ConnectionEntry connection, CredentialEntry credential);
    void Close(Guid sessionId);
    Task PrefetchDnsAsync(IEnumerable<ConnectionEntry> entries, CancellationToken ct = default);
    Task TcpPrewarmAsync(ConnectionEntry entry, CancellationToken ct = default);
}

public class SessionManager : ISessionManager
{
    private readonly IRdpBackend _rdpBackend;
    private readonly ConcurrentDictionary<Guid, IRdpSession> _rdpSessions = new();
    private readonly ConcurrentDictionary<Guid, ISshSession> _sshSessions = new();

    public SessionManager(IRdpBackend rdpBackend) => _rdpBackend = rdpBackend;

    public IRdpSession GetOrCreateRdp(ConnectionEntry connection, CredentialEntry credential) =>
        _rdpSessions.GetOrAdd(connection.Id, _ => _rdpBackend.CreateSession(connection, credential));

    public ISshSession GetOrCreateSsh(ConnectionEntry connection, CredentialEntry credential) =>
        _sshSessions.GetOrAdd(connection.Id, _ => new SshSession(connection, credential));

    public void Close(Guid sessionId)
    {
        if (_rdpSessions.TryRemove(sessionId, out var rdp))
        {
            rdp.DisconnectAsync().GetAwaiter().GetResult();
            rdp.Dispose();
        }
        if (_sshSessions.TryRemove(sessionId, out var ssh))
        {
            ssh.DisconnectAsync().GetAwaiter().GetResult();
            ssh.Dispose();
        }
    }

    /// <summary>
    /// DNS pre-fetch on startup. Parallel resolution — reduces first-connect latency.
    /// </summary>
    public async Task PrefetchDnsAsync(IEnumerable<ConnectionEntry> entries, CancellationToken ct = default)
    {
        await Parallel.ForEachAsync(entries, ct, async (entry, t) =>
        {
            try { await Dns.GetHostAddressesAsync(entry.Host, t); }
            catch { /* Ignore DNS failures during prefetch */ }
        });
    }

    /// <summary>
    /// Begins the TCP handshake on hover so the first packet is already sent
    /// by the time the user clicks Connect. Cancelled if not used.
    /// </summary>
    public async Task TcpPrewarmAsync(ConnectionEntry entry, CancellationToken ct = default)
    {
        try
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(entry.Host, entry.Port, ct);
        }
        catch
        {
            // Prewarm is best-effort
        }
    }
}
