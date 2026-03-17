using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Views;

public partial class VncView : UserControl
{
    private DispatcherTimer? _toolbarHideTimer;
    private Border? _toolbar;

    public VncView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _toolbar = this.FindControl<Border>("VncToolbar");
        var image = this.FindControl<Image>("VncImage");

        _toolbarHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _toolbarHideTimer.Tick += (_, _) =>
        {
            _toolbarHideTimer.Stop();
            if (_toolbar is not null) _toolbar.IsVisible = false;
        };

        if (image is not null)
        {
            image.PointerMoved  += OnPointerMoved;
            image.PointerExited += OnPointerExited;
        }

        if (DataContext is SessionTabViewModel vm)
            vm.FrameBitmapChanged += bitmap =>
                Dispatcher.UIThread.Post(() => { if (image is not null) image.Source = bitmap; });
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
}
