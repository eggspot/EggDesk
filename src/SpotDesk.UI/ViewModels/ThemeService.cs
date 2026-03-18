using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SpotDesk.UI.ViewModels;

public enum AppTheme { Dark, Light, System }

public class ThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void SetTheme(AppTheme theme)
    {
        Current = theme;
        // Guard: skip Avalonia property access if no app or not on the UI thread.
        // Both cases arise in unit tests that don't use [AvaloniaFact].
        if (Application.Current is not { } app) return;
        if (!Dispatcher.UIThread.CheckAccess()) return;
        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}
