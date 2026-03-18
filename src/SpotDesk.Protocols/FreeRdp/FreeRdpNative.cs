using System.Runtime.InteropServices;

namespace SpotDesk.Protocols.FreeRdp;

/// <summary>
/// P/Invoke declarations for FreeRDP 3.x.
/// Same file is used on macOS (libfreerdp3.dylib) and Linux (libfreerdp3.so.3).
/// </summary>
internal static partial class FreeRdpNative
{
    private static string LibName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "libfreerdp3.dylib"
            : "libfreerdp3.so.3";

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_new")]
    internal static partial IntPtr freerdp_new();

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_free")]
    internal static partial void freerdp_free(IntPtr instance);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_connect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_connect(IntPtr instance);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_disconnect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_disconnect(IntPtr instance);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_settings_set_string",
        StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_settings_set_string(IntPtr settings, uint id, string value);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_settings_set_uint32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_settings_set_uint32(IntPtr settings, uint id, uint value);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_settings_get_pointer")]
    internal static partial IntPtr freerdp_settings_get_pointer(IntPtr instance);

    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_update_get_pointer")]
    internal static partial IntPtr freerdp_update_get_pointer(IntPtr instance);

    /// <summary>Returns the rdpInput* sub-system pointer for sending keyboard/mouse events.</summary>
    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_get_sub_system")]
    internal static partial IntPtr freerdp_input_get(IntPtr instance);

    /// <summary>Sends a keyboard scancode event. flags = KBD_FLAGS_DOWN | KBD_FLAGS_UP | KBD_FLAGS_EXTENDED.</summary>
    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_input_send_keyboard_event")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_input_send_keyboard_event(IntPtr input, ushort flags, ushort code);

    /// <summary>Sends a mouse event. flags = PTR_FLAGS_MOVE | PTR_FLAGS_DOWN | PTR_FLAGS_BUTTON1/2/3.</summary>
    [LibraryImport("libfreerdp3", EntryPoint = "freerdp_input_send_mouse_event")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool freerdp_input_send_mouse_event(IntPtr input, ushort flags, ushort x, ushort y);

    // Keyboard event flags (freerdp/input.h)
    internal const ushort KBD_FLAGS_EXTENDED = 0x0100;
    internal const ushort KBD_FLAGS_DOWN     = 0x4000;
    internal const ushort KBD_FLAGS_UP       = 0x8000;

    // Mouse event flags (freerdp/input.h)
    internal const ushort PTR_FLAGS_MOVE    = 0x0800;
    internal const ushort PTR_FLAGS_DOWN    = 0x8000;
    internal const ushort PTR_FLAGS_BUTTON1 = 0x1000; // Left
    internal const ushort PTR_FLAGS_BUTTON2 = 0x2000; // Right
    internal const ushort PTR_FLAGS_BUTTON3 = 0x4000; // Middle

    // Scancodes for Ctrl+Alt+Del sequence (XT set-1)
    internal const ushort SCANCODE_CTRL   = 0x1D;
    internal const ushort SCANCODE_ALT    = 0x38;
    internal const ushort SCANCODE_DELETE = 0x53;

    // FreeRDP settings IDs (subset)
    internal const uint FreeRDP_ServerHostname = 1194;
    internal const uint FreeRDP_ServerPort = 1197;
    internal const uint FreeRDP_Username = 1215;
    internal const uint FreeRDP_Password = 1216;
    internal const uint FreeRDP_DesktopWidth = 1198;
    internal const uint FreeRDP_DesktopHeight = 1199;
    internal const uint FreeRDP_ColorDepth = 1200;
}
