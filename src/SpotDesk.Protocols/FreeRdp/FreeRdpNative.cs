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

    // FreeRDP settings IDs (subset)
    internal const uint FreeRDP_ServerHostname = 1194;
    internal const uint FreeRDP_ServerPort = 1197;
    internal const uint FreeRDP_Username = 1215;
    internal const uint FreeRDP_Password = 1216;
    internal const uint FreeRDP_DesktopWidth = 1198;
    internal const uint FreeRDP_DesktopHeight = 1199;
    internal const uint FreeRDP_ColorDepth = 1200;
}
