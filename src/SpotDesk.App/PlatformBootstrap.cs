using System.Runtime.InteropServices;

namespace SpotDesk.App;

/// <summary>
/// Platform-specific initialization that must run before Avalonia starts.
/// </summary>
public static class PlatformBootstrap
{
    public static void Initialize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            InitWindows();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            InitMacOs();
        else
            InitLinux();
    }

    private static void InitWindows()
    {
        // Set DPI awareness for crisp rendering on high-DPI displays
        SetProcessDpiAwarenessContext(-4); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    }

    private static void InitMacOs()
    {
        // macOS-specific init (e.g., macOS 14+ API surface)
    }

    private static void InitLinux()
    {
        // Ensure DISPLAY or WAYLAND_DISPLAY is set; warn if neither
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        var wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        if (string.IsNullOrEmpty(display) && string.IsNullOrEmpty(wayland))
            Console.Error.WriteLine("SpotDesk: No display server detected ($DISPLAY and $WAYLAND_DISPLAY are unset).");
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(int value);
}
