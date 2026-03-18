using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SpotDesk.Core.Models;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Dialogs;

public partial class NewConnectionDialog : Window
{
    public ConnectionEntry?   ResultEntry      { get; private set; }
    public CredentialEntry?   ResultCredential { get; private set; }

    public NewConnectionDialog()
    {
        InitializeComponent();
        DataContext = new NewConnectionDialogViewModel();
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not NewConnectionDialogViewModel vm) return;
        ResultEntry      = vm.BuildEntry();
        ResultCredential = vm.BuildCredential();
        Close(true);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}
