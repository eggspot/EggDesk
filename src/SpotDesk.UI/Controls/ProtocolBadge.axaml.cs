using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.Controls;

/// <summary>
/// Renders a colored protocol tag: [RDP], [SSH], [VNC].
/// Set the Protocol attached property or bind to it.
/// </summary>
public partial class ProtocolBadge : UserControl
{
    public static readonly StyledProperty<Protocol> ProtocolProperty =
        AvaloniaProperty.Register<ProtocolBadge, Protocol>(nameof(Protocol));

    public Protocol Protocol
    {
        get => GetValue(ProtocolProperty);
        set => SetValue(ProtocolProperty, value);
    }

    public ProtocolBadge()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProtocolProperty)
            UpdateBadge((Protocol)change.NewValue!);
    }

    private void UpdateBadge(Protocol protocol)
    {
        var label  = this.FindControl<TextBlock>("BadgeLabel");
        var border = this.FindControl<Border>("BadgeBorder");
        if (label is null || border is null) return;

        (label.Text, var colorHex) = protocol switch
        {
            Protocol.Rdp => ("RDP", "#1D4ED8"),
            Protocol.Ssh => ("SSH", "#065F46"),
            Protocol.Vnc => ("VNC", "#7C3AED"),
            _            => ("???", "#374151")
        };

        if (Color.TryParse(colorHex, out var c))
            border.Background = new SolidColorBrush(c, 0.3);
    }
}
