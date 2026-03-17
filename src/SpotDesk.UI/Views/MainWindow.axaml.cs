using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SpotDesk.UI.Controls;
using SpotDesk.UI.Dialogs;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Wire drag region — the tab bar area (excluding interactive controls) moves the window
        var titleBar = this.FindControl<Border>("TitleBar");
        titleBar?.AddHandler(PointerPressedEvent, OnTitleBarPointerPressed, handledEventsToo: false);

        // Wire custom window buttons
        var minimize = this.FindControl<Button>("MinimizeButton");
        var maximize = this.FindControl<Button>("MaximizeButton");
        var close    = this.FindControl<Button>("CloseButton");

        if (minimize != null) minimize.Click += (_, _) => WindowState = WindowState.Minimized;
        if (maximize != null) maximize.Click += (_, _) => ToggleMaximize();
        if (close    != null) close.Click    += (_, _) => Close();

        // Keep maximize icon in sync with window state
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
                UpdateMaximizeIcon();
        };

        if (DataContext is not MainWindowViewModel vm) return;

        vm.NewConnectionRequested += OnNewConnectionRequested;
        vm.SettingsRequested      += OnSettingsRequested;
        vm.SearchOpenRequested    += OnSearchOpenRequested;
        vm.GitHubSignInRequested  += OnGitHubSignInRequested;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateMaximizeIcon()
    {
        var icon = this.FindControl<TextBlock>("MaximizeIcon");
        if (icon != null)
            icon.Text = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private async void OnNewConnectionRequested()
    {
        var dialog = new NewConnectionDialog();
        await dialog.ShowDialog(this);
    }

    private async void OnSettingsRequested()
    {
        if (DataContext is MainWindowViewModel)
        {
            var settingsVm = AppServices.GetRequired<SettingsViewModel>();
            var dialog = new Window
            {
                Title                 = "Settings",
                Width                 = 720,
                Height                = 540,
                Background            = this.Background,
                Content               = new SettingsView { DataContext = settingsVm },
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            await dialog.ShowDialog(this);
        }
    }

    private async void OnGitHubSignInRequested()
    {
        var dialog = new OAuthConnectDialog();
        await dialog.ShowDialog(this);
    }

    private void OnSearchOpenRequested()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var searchBox = new SearchBox { DataContext = vm.Search };
        if (Content is Panel rootPanel)
            rootPanel.Children.Add(searchBox);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
            e.Handled = true;
        }
    }
}
