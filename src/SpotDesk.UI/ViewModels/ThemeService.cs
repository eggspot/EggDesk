using Avalonia;
using Avalonia.Styling;

namespace SpotDesk.UI.ViewModels;

public enum AppTheme { Dark, Light, System }

public class ThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void SetTheme(AppTheme theme)
    {
        Current = theme;
        var app = Application.Current!;
        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.System => ThemeVariant.Default,
            _ => ThemeVariant.Dark
        };
    }
}
