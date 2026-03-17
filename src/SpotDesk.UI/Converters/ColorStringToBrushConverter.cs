using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SpotDesk.UI.Converters;

/// <summary>Converts a hex color string (e.g. "#22C55E") to a SolidColorBrush.</summary>
public sealed class ColorStringToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && Color.TryParse(hex, out var color))
            return new SolidColorBrush(color);
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
