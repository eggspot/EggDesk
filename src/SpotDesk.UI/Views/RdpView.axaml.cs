using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Views;

/// <summary>
/// RDP session view.
/// Windows: embeds a NativeControlHost that hosts the ActiveX MSTSC control.
/// macOS/Linux: renders the bitmap stream from FreeRDP into an Image control.
/// The session toolbar auto-hides after 2 seconds on pointer leave.
/// </summary>
public partial class RdpView : UserControl
{
    private DispatcherTimer? _toolbarHideTimer;
    private Border? _toolbar;
    private Panel?  _surface;

    public RdpView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _toolbar = this.FindControl<Border>("SessionToolbar");
        _surface = this.FindControl<Panel>("SessionSurface");

        _toolbarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _toolbarHideTimer.Tick += (_, _) =>
        {
            _toolbarHideTimer.Stop();
            if (_toolbar is not null) _toolbar.IsVisible = false;
        };

        if (_surface is not null)
        {
            _surface.PointerMoved  += OnPointerMoved;
            _surface.PointerExited += OnPointerExited;
        }

        // Wire up toolbar buttons
        var btnFit       = this.FindControl<Button>("BtnFitWindow");
        var btnFull      = this.FindControl<Button>("BtnFullScreen");
        var btnScreenshot = this.FindControl<Button>("BtnScreenshot");
        var btnCad       = this.FindControl<Button>("BtnCtrlAltDel");

        if (btnFit       is not null) btnFit.Click       += (_, _) => FitWindow();
        if (btnFull      is not null) btnFull.Click      += (_, _) => ToggleFullScreen();
        if (btnScreenshot is not null) btnScreenshot.Click += (_, _) => TakeScreenshot();
        if (btnCad       is not null) btnCad.Click       += (_, _) => SendCtrlAltDel();

        AttachSessionSurface();
    }

    private void AttachSessionSurface()
    {
        if (_surface is null) return;

        if (OperatingSystem.IsWindows())
        {
            // On Windows embed the MSTSC ActiveX via NativeControlHost.
            // The actual wiring into the RDP backend is handled by WindowsRdpBackend.
            var host = new NativeControlHost
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch
            };
            _surface.Children.Add(host);
        }
        else
        {
            // On macOS/Linux render the frame bitmap from FreeRDP.
            var img = new Image
            {
                Stretch             = Avalonia.Media.Stretch.Fill,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Stretch
            };
            _surface.Children.Add(img);

            if (DataContext is SessionTabViewModel vm)
                vm.FrameBitmapChanged += bitmap => Dispatcher.UIThread.Post(() => img.Source = bitmap);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_toolbar is not null) _toolbar.IsVisible = true;
        _toolbarHideTimer?.Stop();
        _toolbarHideTimer?.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _toolbarHideTimer?.Stop();
        _toolbarHideTimer?.Start();
    }

    private void FitWindow()
    {
        // Notify the ViewModel to resize the session to match current bounds
        if (DataContext is SessionTabViewModel vm)
            vm.FitWindowCommand.Execute(null);
    }

    private void ToggleFullScreen()
    {
        var window = TopLevel.GetTopLevel(this) as Window;
        if (window is null) return;
        window.WindowState = window.WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    private void TakeScreenshot()
    {
        if (DataContext is SessionTabViewModel vm)
            vm.TakeScreenshotCommand.Execute(null);
    }

    private void SendCtrlAltDel()
    {
        if (DataContext is SessionTabViewModel vm)
            vm.SendCtrlAltDelCommand.Execute(null);
    }
}
