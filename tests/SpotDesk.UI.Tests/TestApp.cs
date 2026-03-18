using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(SpotDesk.UI.Tests.TestAppBuilder))]

namespace SpotDesk.UI.Tests;

/// <summary>
/// Minimal Avalonia app used for all M4 headless tests.
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

public class TestApp : Application;
