using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SpotDesk.UI.Converters;

/// <summary>
/// Maps latency (ms int) to a color:
///   &lt; 50ms  → green  #22C55E
///   &lt; 150ms → amber  #F59E0B
///   else    → red    #EF4444
/// </summary>
public sealed class LatencyColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green  = new(Color.Parse("#22C55E"));
    private static readonly SolidColorBrush Amber  = new(Color.Parse("#F59E0B"));
    private static readonly SolidColorBrush Red    = new(Color.Parse("#EF4444"));
    private static readonly SolidColorBrush Gray   = new(Color.Parse("#6B7280"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int ms)
        {
            if (ms <= 0)   return Gray;
            if (ms < 50)   return Green;
            if (ms < 150)  return Amber;
            return Red;
        }
        return Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
