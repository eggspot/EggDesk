using System.Globalization;
using Avalonia.Data.Converters;

namespace SpotDesk.UI.Converters;

/// <summary>
/// Returns 240.0 when the sidebar is visible, 0.0 when hidden.
/// Bound to IsSidebarVisible on MainWindowViewModel.
/// </summary>
public sealed class SidebarWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 240.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
