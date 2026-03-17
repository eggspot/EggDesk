using SpotDesk.Core.Models;

namespace SpotDesk.Protocols;

/// <summary>
/// Platform-specific RDP backend factory. Inject the correct implementation at startup.
/// Windows → WindowsRdpBackend; macOS/Linux → FreeRdpBackend.
/// </summary>
public interface IRdpBackend
{
    IRdpSession CreateSession(ConnectionEntry connection, CredentialEntry credential);
}
