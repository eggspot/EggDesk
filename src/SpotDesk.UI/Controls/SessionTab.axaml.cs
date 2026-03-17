using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Controls;

public partial class SessionTab : UserControl
{
    public SessionTab()
    {
        InitializeComponent();
        // Middle-click to close
        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed
            && DataContext is SessionTabViewModel vm)
        {
            vm.CloseCommand.Execute(null);
        }
    }
}
