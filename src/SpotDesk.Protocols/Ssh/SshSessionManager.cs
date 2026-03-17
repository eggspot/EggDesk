using System.Collections.Concurrent;
using SpotDesk.Core.Models;

namespace SpotDesk.Protocols.Ssh;

public interface ISshSessionManager
{
    ISshSession GetOrCreate(ConnectionEntry connection, CredentialEntry credential);
    void Close(Guid sessionId);
}

public class SshSessionManager : ISshSessionManager
{
    private readonly ConcurrentDictionary<Guid, ISshSession> _sessions = new();

    public ISshSession GetOrCreate(ConnectionEntry connection, CredentialEntry credential) =>
        _sessions.GetOrAdd(connection.Id, _ => new SshSession(connection, credential));

    public void Close(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.DisconnectAsync().GetAwaiter().GetResult();
            session.Dispose();
        }
    }
}
