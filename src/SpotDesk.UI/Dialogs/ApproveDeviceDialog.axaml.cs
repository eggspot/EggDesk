using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SpotDesk.UI.Dialogs;

public partial class ApproveDeviceDialog : Window
{
    public bool Approved { get; private set; }

    public ApproveDeviceDialog()
    {
        InitializeComponent();
    }

    public ApproveDeviceDialog(string deviceId, string deviceName) : this()
    {
        var nameLabel = this.FindControl<TextBlock>("DeviceName");
        var idLabel   = this.FindControl<TextBlock>("DeviceId");
        var timeLabel = this.FindControl<TextBlock>("RequestTime");

        if (nameLabel is not null) nameLabel.Text = deviceName;
        if (idLabel   is not null) idLabel.Text   = deviceId;
        if (timeLabel is not null) timeLabel.Text  = DateTimeOffset.Now.ToString("g");
    }

    private void OnApprove(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Approved = true;
        Close(true);
    }

    private void OnDeny(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Approved = false;
        Close(false);
    }
}
