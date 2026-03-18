using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SpotDesk.Core.Models;

namespace SpotDesk.Protocols.FreeRdp;

/// <summary>
/// FreeRDP 3.x backend for macOS and Linux.
/// Shared implementation — platform differences are in the native library name only.
/// </summary>
[UnsupportedOSPlatform("windows")]
public class FreeRdpBackend : IRdpBackend
{
    public IRdpSession CreateSession(ConnectionEntry connection, CredentialEntry credential) =>
        new FreeRdpSession(connection, credential);
}

[UnsupportedOSPlatform("windows")]
public class FreeRdpSession : IRdpSession
{
    private readonly ConnectionEntry _connection;
    private readonly CredentialEntry _credential;
    private IntPtr _instance;
    private WriteableBitmap? _framebuffer;

    public Guid Id { get; } = Guid.NewGuid();
    public SessionStatus Status { get; private set; } = SessionStatus.Idle;
    public int LatencyMs { get; private set; }
    public string? Codec { get; private set; } = "RDP";

    public event Action<SessionStatus> StatusChanged = delegate { };
    public event Action<int> LatencyUpdated = delegate { };
    public event Action FrameUpdated = delegate { };

    public FreeRdpSession(ConnectionEntry connection, CredentialEntry credential)
    {
        _connection = connection;
        _credential = credential;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        SetStatus(SessionStatus.Connecting);

        await Task.Run(() =>
        {
            _instance = FreeRdpNative.freerdp_new();
            if (_instance == IntPtr.Zero)
                throw new InvalidOperationException("freerdp_new() returned null.");

            var settings = FreeRdpNative.freerdp_settings_get_pointer(_instance);
            FreeRdpNative.freerdp_settings_set_string(settings, FreeRdpNative.FreeRDP_ServerHostname, _connection.Host);
            FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_ServerPort, (uint)_connection.Port);
            FreeRdpNative.freerdp_settings_set_string(settings, FreeRdpNative.FreeRDP_Username, _credential.Username);
            if (_credential.Password is not null)
                FreeRdpNative.freerdp_settings_set_string(settings, FreeRdpNative.FreeRDP_Password, _credential.Password);

            var w = (uint)(_connection.DesktopWidth ?? 1920);
            var h = (uint)(_connection.DesktopHeight ?? 1080);
            FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_DesktopWidth, w);
            FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_DesktopHeight, h);
            FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_ColorDepth, (uint)_connection.ColorDepth);

            if (!FreeRdpNative.freerdp_connect(_instance))
                throw new InvalidOperationException($"FreeRDP failed to connect to {_connection.Host}:{_connection.Port}");

            // Allocate framebuffer
            _framebuffer = new WriteableBitmap(
                new Avalonia.PixelSize((int)w, (int)h),
                new Avalonia.Vector(96, 96),
                PixelFormats.Bgra8888);
        }, ct);

        SetStatus(SessionStatus.Connected);
    }

    public Task DisconnectAsync()
    {
        SetStatus(SessionStatus.Disconnecting);
        if (_instance != IntPtr.Zero)
        {
            FreeRdpNative.freerdp_disconnect(_instance);
            FreeRdpNative.freerdp_free(_instance);
            _instance = IntPtr.Zero;
        }
        SetStatus(SessionStatus.Idle);
        return Task.CompletedTask;
    }

    public WriteableBitmap? GetFrameBuffer() => _framebuffer;

    private IntPtr GetInput() =>
        _instance != IntPtr.Zero ? FreeRdpNative.freerdp_input_get(_instance) : IntPtr.Zero;

    public void SendKeyDown(int scanCode, bool isExtended = false)
    {
        var input = GetInput();
        if (input == IntPtr.Zero) return;
        ushort flags = FreeRdpNative.KBD_FLAGS_DOWN;
        if (isExtended) flags |= FreeRdpNative.KBD_FLAGS_EXTENDED;
        FreeRdpNative.freerdp_input_send_keyboard_event(input, flags, (ushort)scanCode);
    }

    public void SendKeyUp(int scanCode, bool isExtended = false)
    {
        var input = GetInput();
        if (input == IntPtr.Zero) return;
        ushort flags = FreeRdpNative.KBD_FLAGS_UP;
        if (isExtended) flags |= FreeRdpNative.KBD_FLAGS_EXTENDED;
        FreeRdpNative.freerdp_input_send_keyboard_event(input, flags, (ushort)scanCode);
    }

    public void SendMouseMove(int x, int y)
    {
        var input = GetInput();
        if (input == IntPtr.Zero) return;
        FreeRdpNative.freerdp_input_send_mouse_event(
            input, FreeRdpNative.PTR_FLAGS_MOVE, (ushort)x, (ushort)y);
    }

    public void SendMouseButton(MouseButton button, bool isDown)
    {
        var input = GetInput();
        if (input == IntPtr.Zero) return;
        ushort btnFlag = button switch
        {
            MouseButton.Left   => FreeRdpNative.PTR_FLAGS_BUTTON1,
            MouseButton.Right  => FreeRdpNative.PTR_FLAGS_BUTTON2,
            MouseButton.Middle => FreeRdpNative.PTR_FLAGS_BUTTON3,
            _                  => 0,
        };
        if (btnFlag == 0) return;
        ushort flags = btnFlag;
        if (isDown) flags |= FreeRdpNative.PTR_FLAGS_DOWN;
        FreeRdpNative.freerdp_input_send_mouse_event(input, flags, 0, 0);
    }

    public void SendCtrlAltDel()
    {
        var input = GetInput();
        if (input == IntPtr.Zero) return;
        // Press Ctrl + Alt + Delete, then release in reverse order
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_DOWN, FreeRdpNative.SCANCODE_CTRL);
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_DOWN | FreeRdpNative.KBD_FLAGS_EXTENDED, FreeRdpNative.SCANCODE_ALT);
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_DOWN | FreeRdpNative.KBD_FLAGS_EXTENDED, FreeRdpNative.SCANCODE_DELETE);
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_UP   | FreeRdpNative.KBD_FLAGS_EXTENDED, FreeRdpNative.SCANCODE_DELETE);
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_UP   | FreeRdpNative.KBD_FLAGS_EXTENDED, FreeRdpNative.SCANCODE_ALT);
        FreeRdpNative.freerdp_input_send_keyboard_event(input, FreeRdpNative.KBD_FLAGS_UP,                                      FreeRdpNative.SCANCODE_CTRL);
    }

    public void Resize(int width, int height)
    {
        if (_instance == IntPtr.Zero) return;
        var settings = FreeRdpNative.freerdp_settings_get_pointer(_instance);
        FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_DesktopWidth, (uint)width);
        FreeRdpNative.freerdp_settings_set_uint32(settings, FreeRdpNative.FreeRDP_DesktopHeight, (uint)height);
        _framebuffer = new WriteableBitmap(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormats.Bgra8888);
    }

    private void SetStatus(SessionStatus s)
    {
        Status = s;
        StatusChanged(s);
    }

    public void Dispose()
    {
        if (_instance != IntPtr.Zero)
        {
            FreeRdpNative.freerdp_free(_instance);
            _instance = IntPtr.Zero;
        }
        _framebuffer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
