using Avalonia.Media.Imaging;
using SpotDesk.Core.Models;

namespace SpotDesk.Protocols;

public interface IRdpSession : IDisposable
{
    Guid Id { get; }
    SessionStatus Status { get; }
    int LatencyMs { get; }
    string? Codec { get; }

    event Action<SessionStatus> StatusChanged;
    event Action<int> LatencyUpdated;
    event Action FrameUpdated;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    /// <summary>Current frame buffer. Only valid when Connected.</summary>
    WriteableBitmap? GetFrameBuffer();

    void SendKeyDown(int scanCode, bool isExtended = false);
    void SendKeyUp(int scanCode, bool isExtended = false);
    void SendMouseMove(int x, int y);
    void SendMouseButton(MouseButton button, bool isDown);
    void SendCtrlAltDel();
    void Resize(int width, int height);
}

public enum MouseButton { Left, Right, Middle }
