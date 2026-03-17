using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.ViewModels;

public partial class SessionTabViewModel : ObservableObject, IDisposable
{
    private const int ReconnectDelaySeconds = 3;

    public Guid ConnectionId { get; }

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private Protocol _protocol;
    [ObservableProperty] private SessionStatus _status = SessionStatus.Idle;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private int _latencyMs;
    [ObservableProperty] private string? _codec;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _reconnectCountdown;
    [ObservableProperty] private bool _isReconnecting;

    private System.Threading.Timer? _reconnectTimer;
    private CancellationTokenSource? _reconnectCts;

    // ── Derived display ───────────────────────────────────────────────────

    public string StatusColor => Status switch
    {
        SessionStatus.Connected                       => "#22C55E",
        SessionStatus.Connecting or
        SessionStatus.Reconnecting                    => "#F59E0B",
        SessionStatus.Error                           => "#EF4444",
        _                                             => "#6B7280"
    };

    public string ProtocolIcon => Protocol switch
    {
        Protocol.Rdp => "M 2 2 L 14 2 L 14 10 L 2 10 Z",
        Protocol.Ssh => "M 3 8 L 7 4 L 11 8",
        Protocol.Vnc => "M 2 2 L 14 14 M 14 2 L 2 14",
        _            => string.Empty
    };

    // ── SSH status bar properties ─────────────────────────────────────────
    public string StatusText  => StatusMessage ?? Status.ToString();
    public string LatencyText => LatencyMs > 0 ? $"{LatencyMs}ms" : string.Empty;

    // ── RDP/VNC frame bitmap (used by RdpView / VncView) ─────────────────

    public event Action<WriteableBitmap?>? FrameBitmapChanged;

    public void NotifyFrameBitmapChanged(WriteableBitmap? bitmap) =>
        FrameBitmapChanged?.Invoke(bitmap);

    // ── SshTabViewModel compatibility (SshView reads this) ───────────────

    private SpotDesk.Protocols.Ssh.Terminal.TerminalBuffer? _terminalBuffer;
    public SpotDesk.Protocols.Ssh.Terminal.TerminalBuffer TerminalBuffer =>
        _terminalBuffer ??= new SpotDesk.Protocols.Ssh.Terminal.TerminalBuffer();

    // ── Construction ─────────────────────────────────────────────────────

    public SessionTabViewModel(ConnectionEntry connection)
    {
        ConnectionId  = connection.Id;
        _displayName  = connection.Name;
        _protocol     = connection.Protocol;
        _statusMessage = string.Empty;
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConnectAsync()
    {
        _reconnectCts?.Cancel();
        _reconnectTimer?.Dispose();
        IsReconnecting = false;

        Status        = SessionStatus.Connecting;
        StatusMessage = "Connecting…";

        // TODO: get session from ISessionManager and call ConnectAsync
        // Simulated connect for now
        await Task.Delay(100);
        Status        = SessionStatus.Connected;
        StatusMessage = "Connected";
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        Status        = SessionStatus.Disconnecting;
        StatusMessage = "Disconnecting…";
        // TODO: call ISessionManager.Close(ConnectionId)
        await Task.CompletedTask;
        Status        = SessionStatus.Idle;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private Task ReconnectAsync() => ConnectAsync();

    [RelayCommand]
    private void CancelReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectTimer?.Dispose();
        IsReconnecting     = false;
        ReconnectCountdown = 0;
        Status             = SessionStatus.Idle;
        StatusMessage      = string.Empty;
    }

    [RelayCommand]
    private void Close()
    {
        // Handled by MainWindowViewModel.CloseTab — no-op here
    }

    [RelayCommand]
    private void FitWindow()
    {
        // Notify the RDP/VNC backend to resize the session to match the current
        // surface bounds. The backend calls back on FrameBitmapChanged with a
        // resized bitmap.
    }

    [RelayCommand]
    private void TakeScreenshot()
    {
        // TODO: capture the current FrameBitmap and save to Downloads
    }

    [RelayCommand]
    private void SendCtrlAltDel()
    {
        // TODO: forward the key combo to the active session
    }

    // ── Input routing for SSH ─────────────────────────────────────────────

    public void HandleKeyInput(Avalonia.Input.KeyEventArgs e)
    {
        // TODO: translate Avalonia key event → SSH terminal byte sequence
        // and write to the SshSession's stdin pipe.
    }

    // ── Auto-reconnect ────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the underlying session disconnects unexpectedly.
    /// Starts a 3-second countdown, then auto-reconnects.
    /// </summary>
    public void OnUnexpectedDisconnect(string? reason = null)
    {
        Status        = SessionStatus.Error;
        StatusMessage = reason ?? "Connection lost";
        IsReconnecting     = true;
        ReconnectCountdown = ReconnectDelaySeconds;

        _reconnectCts = new CancellationTokenSource();
        var token     = _reconnectCts.Token;

        _reconnectTimer = new System.Threading.Timer(_ =>
        {
            if (token.IsCancellationRequested) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested) return;

                ReconnectCountdown--;
                if (ReconnectCountdown <= 0)
                {
                    _reconnectTimer?.Dispose();
                    _ = ReconnectAsync();
                }
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose()
    {
        _reconnectCts?.Cancel();
        _reconnectTimer?.Dispose();
        // TODO: ensure ISessionManager.Close(ConnectionId)
        GC.SuppressFinalize(this);
    }
}

