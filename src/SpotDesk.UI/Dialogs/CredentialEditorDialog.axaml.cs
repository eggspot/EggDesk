using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.Dialogs;

public partial class CredentialEditorDialog : Window
{
    public CredentialEntry? Result { get; private set; }

    private readonly CredentialEntry? _existing;

    public CredentialEditorDialog() : this(null) { }

    public CredentialEditorDialog(CredentialEntry? existing)
    {
        InitializeComponent();
        _existing = existing;

        DataContext = new { Title = existing is null ? "New Credential" : "Edit Credential" };

        if (existing is not null)
        {
            SetField("NameBox",     existing.Name);
            SetField("UsernameBox", existing.Username);
            SetField("SshKeyBox",   existing.SshKeyPath ?? string.Empty);
        }
    }

    private void SetField(string name, string value)
    {
        if (this.FindControl<TextBox>(name) is TextBox tb)
            tb.Text = value;
    }

    private async void OnBrowseSshKey(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SSH key",
            AllowMultiple = false
        });
        if (files.Count > 0 && this.FindControl<TextBox>("SshKeyBox") is TextBox tb)
            tb.Text = files[0].Path.LocalPath;
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var entry = _existing ?? new SpotDesk.Core.Models.CredentialEntry();
        entry.Name       = GetField("NameBox");
        entry.Username   = GetField("UsernameBox");
        entry.SshKeyPath = NullIfEmpty(GetField("SshKeyBox"));

        var pw = GetField("PasswordBox");
        if (!string.IsNullOrEmpty(pw))
            entry.Password = pw; // Encryption handled by VaultService on persist

        Result = entry;
        Close(true);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private string GetField(string name) =>
        this.FindControl<TextBox>(name)?.Text ?? string.Empty;

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
