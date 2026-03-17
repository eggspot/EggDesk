using System.Globalization;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotDesk.Core.Auth;
using SpotDesk.Core.Vault;

namespace SpotDesk.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IOAuthService _oauth;
    private readonly IVaultService _vault;
    private readonly ISessionLockService _sessionLock;
    private readonly ThemeService _themeService;
    private readonly LocalPrefsService _prefs;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty] private bool _lockOnScreenLock;
    [ObservableProperty] private string _autoSyncInterval = "5 min";
    [ObservableProperty] private string? _gitRemoteUrl;
    [ObservableProperty] private DateTimeOffset? _lastSyncedAt;
    [ObservableProperty] private bool _isGitHubConnected;
    [ObservableProperty] private string? _githubLogin;
    [ObservableProperty] private bool _isVaultUnlocked;
    [ObservableProperty] private string _encryptionInfo = "AES-256-GCM · Argon2id · per-device key envelope";
    [ObservableProperty] private DeviceInfo[] _trustedDevices = [];

    public SettingsViewModel(
        IOAuthService oauth,
        IVaultService vault,
        ISessionLockService sessionLock,
        ThemeService? themeService = null,
        LocalPrefsService? prefs = null)
    {
        _oauth        = oauth;
        _vault        = vault;
        _sessionLock  = sessionLock;
        _themeService = themeService ?? new ThemeService();
        _prefs        = prefs        ?? new LocalPrefsService();
        IsVaultUnlocked = sessionLock.IsUnlocked;

        var saved = _prefs.Load();
        _theme = saved.Theme;
    }

    // ── Theme (live change) ───────────────────────────────────────────────

    partial void OnThemeChanged(AppTheme value)
    {
        _themeService.SetTheme(value);
        _prefs.Save(p => p with { Theme = value });
    }

    // ── GitHub ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ConnectGitHubAsync()
    {
        var identity = await _oauth.AuthenticateGitHubAsync();
        IsGitHubConnected = true;
        GithubLogin = identity.Login;
    }

    [RelayCommand]
    private async Task DisconnectGitHubAsync()
    {
        await _oauth.RevokeAsync(OAuthProvider.GitHub);
        IsGitHubConnected = false;
        GithubLogin = null;
    }

    // ── Vault ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void LockVault()
    {
        _sessionLock.Lock();
        IsVaultUnlocked = false;
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        // TODO: call IGitSyncService.SyncAsync
        LastSyncedAt = DateTimeOffset.UtcNow;
        await Task.CompletedTask;
    }

    // ── Trusted devices ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task RevokeDeviceAsync(string deviceId)
    {
        await _vault.RevokeDeviceAsync(deviceId);
        await LoadTrustedDevicesAsync();
    }

    [RelayCommand]
    private async Task ApproveNewDeviceAsync(DeviceApprovalRequest request)
    {
        await _vault.AddDeviceAsync(request.DeviceId, request.DeviceName);
        await LoadTrustedDevicesAsync();
    }

    private async Task LoadTrustedDevicesAsync()
    {
        // TODO: expose device list from VaultService
        await Task.CompletedTask;
    }
}

public record DeviceInfo(string DeviceId, string DeviceName, DateTimeOffset AddedAt, bool IsCurrentDevice);
public record DeviceApprovalRequest(string DeviceId, string DeviceName);

// ── AppThemeConverter — used in SettingsView XAML radio buttons ───────────

/// <summary>
/// Converts an AppTheme enum to bool for RadioButton.IsChecked bindings.
/// Usage: IsChecked="{Binding Theme, Converter={x:Static vm:AppThemeConverter.Dark}}"
/// </summary>
public sealed class AppThemeConverter : IValueConverter
{
    public static readonly AppThemeConverter Dark   = new(AppTheme.Dark);
    public static readonly AppThemeConverter Light  = new(AppTheme.Light);
    public static readonly AppThemeConverter System = new(AppTheme.System);

    private readonly AppTheme _target;
    private AppThemeConverter(AppTheme target) => _target = target;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AppTheme t && t == _target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
}
