using System.Globalization;
using Avalonia.Data.Converters;

namespace SpotDesk.UI.Converters;

/// <summary>Returns "▾" when expanded, "▸" when collapsed.</summary>
public sealed class ExpandArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "▾" : "▸";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
