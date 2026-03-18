using System.Globalization;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using SpotDesk.Core.Models;

namespace SpotDesk.UI.ViewModels;

public partial class NewConnectionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRdp), nameof(IsSsh), nameof(DefaultPortHint))]
    private Protocol _protocol = Protocol.Rdp;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _name = string.Empty;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _host = string.Empty;

    [ObservableProperty] private int    _port     = 3389;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _group    = string.Empty;
    [ObservableProperty] private string _tags     = string.Empty;

    // SSH
    [ObservableProperty] private string _sshKeyPath = string.Empty;

    // RDP advanced
    [ObservableProperty] private int  _desktopWidth           = 1920;
    [ObservableProperty] private int  _desktopHeight          = 1080;
    [ObservableProperty] private bool _enableDriveRedirection = false;
    [ObservableProperty] private bool _enableRemoteFx         = true;

    public bool IsRdp => Protocol == Protocol.Rdp;
    public bool IsSsh => Protocol == Protocol.Ssh;
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Host);

    public string DefaultPortHint => Protocol switch
    {
        Protocol.Rdp => "3389",
        Protocol.Ssh => "22",
        Protocol.Vnc => "5900",
        _            => "3389",
    };

    partial void OnProtocolChanged(Protocol value)
    {
        Port = ConnectionEntry.DefaultPortFor(value);
    }

    /// <summary>Builds a ConnectionEntry from the current ViewModel state.</summary>
    public ConnectionEntry BuildEntry() => new()
    {
        Name                  = Name.Trim(),
        Host                  = Host.Trim(),
        Port                  = Port,
        Protocol              = Protocol,
        DesktopWidth          = IsRdp ? DesktopWidth  : null,
        DesktopHeight         = IsRdp ? DesktopHeight : null,
        EnableRemoteFx        = EnableRemoteFx,
        EnableDriveRedirection = EnableDriveRedirection,
        Tags                  = Tags.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        UpdatedAt             = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Builds an in-memory CredentialEntry from the entered credentials.
    /// Returns null if neither username nor SSH key was provided.
    /// </summary>
    public CredentialEntry? BuildCredential()
    {
        var hasUser   = !string.IsNullOrWhiteSpace(Username);
        var hasSshKey = !string.IsNullOrWhiteSpace(SshKeyPath);

        if (!hasUser && !hasSshKey) return null;

        return new CredentialEntry
        {
            Name       = Name.Trim().Length > 0 ? Name.Trim() : Host.Trim(),
            Username   = Username.Trim(),
            Password   = string.IsNullOrEmpty(Password) ? null : Password,
            SshKeyPath = hasSshKey ? SshKeyPath.Trim() : null,
            Type       = hasSshKey ? CredentialType.SshKey : CredentialType.UsernamePassword,
            UpdatedAt  = DateTimeOffset.UtcNow,
        };
    }
}

/// <summary>
/// Converts Protocol enum to bool for ToggleButton.IsChecked bindings.
/// Usage: IsChecked="{Binding Protocol, Converter={x:Static vm:ProtocolConverter.Rdp}}"
/// </summary>
public sealed class ProtocolConverter : IValueConverter
{
    public static readonly ProtocolConverter Rdp = new(Protocol.Rdp);
    public static readonly ProtocolConverter Ssh = new(Protocol.Ssh);
    public static readonly ProtocolConverter Vnc = new(Protocol.Vnc);

    private readonly Protocol _target;
    private ProtocolConverter(Protocol target) => _target = target;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Protocol p && p == _target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
}
