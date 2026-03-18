using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SpotDesk.UI.Converters;

/// <summary>
/// Returns the accent brush when the tab is active (IsActive == true),
/// otherwise Transparent — used for the 2px bottom-border tab indicator.
/// </summary>
public sealed class ActiveTabBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#3B82F6"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? AccentBrush : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
