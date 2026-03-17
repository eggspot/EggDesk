using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Controls;

/// <summary>
/// Global search overlay (Ctrl+K).
/// Keyboard navigation: Up/Down arrow selects items, Enter activates,
/// Escape closes the overlay.
/// </summary>
public partial class SearchBox : UserControl
{
    public SearchBox()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("SearchInput");
        if (input is not null)
        {
            input.Focus();
            input.KeyDown += OnInputKeyDown;
        }

        var backdrop = this.FindControl<Border>("Backdrop");
        if (backdrop is not null)
            backdrop.PointerPressed += (_, _) => CloseOverlay();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SearchViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                CloseOverlay();
                e.Handled = true;
                break;

            case Key.Down:
                vm.SelectNextCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                vm.SelectPreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                vm.ActivateSelectedCommand.Execute(null);
                CloseOverlay();
                e.Handled = true;
                break;
        }
    }

    private void CloseOverlay()
    {
        if (DataContext is SearchViewModel vm)
            vm.CloseCommand.Execute(null);

        // Remove from parent
        if (Parent is Panel panel)
            panel.Children.Remove(this);
    }
}
