using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SpotDesk.UI.ViewModels;
using SpotDesk.UI.Views;

namespace SpotDesk.UI;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = AppServices.GetRequired<MainWindowViewModel>()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Thin service locator bridge — DI is configured in SpotDesk.App/Program.cs.</summary>
public static class AppServices
{
    private static IServiceProvider? _provider;

    public static void Configure(IServiceProvider provider) => _provider = provider;

    public static T GetRequired<T>() where T : notnull =>
        _provider is not null
            ? (T)_provider.GetService(typeof(T))!
            : throw new InvalidOperationException("AppServices not configured.");
}
