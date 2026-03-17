using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SpotDesk.UI.Dialogs;

public partial class MasterPasswordSetupDialog : Window
{
    /// <summary>The password the user chose, or null if skipped.</summary>
    public string? ChosenPassword { get; private set; }

    public MasterPasswordSetupDialog()
    {
        InitializeComponent();
    }

    private void OnSetPassword(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pw      = this.FindControl<TextBox>("PasswordBox")?.Text ?? string.Empty;
        var confirm = this.FindControl<TextBox>("ConfirmBox")?.Text ?? string.Empty;
        var error   = this.FindControl<TextBlock>("ErrorLabel");

        if (pw.Length < 8)
        {
            ShowError("Password must be at least 8 characters.", error);
            return;
        }

        if (pw != confirm)
        {
            ShowError("Passwords do not match.", error);
            return;
        }

        ChosenPassword = pw;
        Close(true);
    }

    private void OnSkip(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ChosenPassword = null;
        Close(false);
    }

    private static void ShowError(string message, TextBlock? label)
    {
        if (label is null) return;
        label.Text      = message;
        label.IsVisible = true;
    }
}
