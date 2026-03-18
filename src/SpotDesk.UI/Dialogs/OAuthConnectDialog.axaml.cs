using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SpotDesk.Core.Auth;

namespace SpotDesk.UI.Dialogs;

public partial class OAuthConnectDialog : Window
{
    private readonly IOAuthService _oauth;
    private CancellationTokenSource? _deviceFlowCts;

    public OAuthConnectDialog() : this(AppServices.GetRequired<IOAuthService>()) { }

    public OAuthConnectDialog(IOAuthService oauth)
    {
        InitializeComponent();
        _oauth = oauth;

        this.FindControl<Button>("GitHubButton")!        .Click += (_, _) => _ = StartGitHubDeviceFlowAsync();
        this.FindControl<Button>("BitbucketButton")!    .Click += (_, _) => ShowPanel("bitbucket");
        this.FindControl<Button>("MasterPasswordButton")!.Click += (_, _) => Close(null);
        this.FindControl<Button>("CancelFlowButton")!   .Click += (_, _) => CancelFlow();
        this.FindControl<Button>("ConnectBitbucketButton")!.Click += (_, _) => _ = ConnectBitbucketAsync();
        this.FindControl<Button>("BackFromBitbucketButton")!.Click += (_, _) => ShowPanel("initial");
    }

    // ── GitHub Device Flow ───────────────────────────────────────────────────

    private async Task StartGitHubDeviceFlowAsync()
    {
        ShowPanel("github");
        _deviceFlowCts = new CancellationTokenSource();
        var ct = _deviceFlowCts.Token;

        var status = this.FindControl<TextBlock>("FlowStatusLabel")!;
        var codeLabel = this.FindControl<TextBlock>("DeviceCodeLabel")!;

        status.Text = "Requesting code…";

        try
        {
            var challenge = await _oauth.StartGitHubDeviceFlowAsync(ct);

            codeLabel.Text = challenge.UserCode;
            status.Text    = "Waiting for approval…";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = challenge.VerificationUri, UseShellExecute = true
            });

            var identity = await _oauth.PollGitHubDeviceFlowAsync(challenge, ct);
            status.Text  = $"Signed in as {identity.Login} ✓";
            await Task.Delay(1000, CancellationToken.None);
            Close(identity);
        }
        catch (OperationCanceledException)
        {
            ShowPanel("initial");
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brushes.OrangeRed;
            status.Text       = $"Error: {ex.Message}";
            this.FindControl<Button>("CancelFlowButton")!.Content = "Back";
        }
    }

    private void CancelFlow()
    {
        _deviceFlowCts?.Cancel();
        _deviceFlowCts = null;
        ShowPanel("initial");
    }

    // ── Bitbucket App Password ────────────────────────────────────────────────

    private async Task ConnectBitbucketAsync()
    {
        var statusLabel = this.FindControl<TextBlock>("BitbucketStatusLabel")!;
        var connectBtn  = this.FindControl<Button>("ConnectBitbucketButton")!;
        var username    = this.FindControl<TextBox>("BitbucketUsernameBox")!.Text ?? string.Empty;
        var appPassword = this.FindControl<TextBox>("BitbucketPasswordBox")!.Text ?? string.Empty;

        statusLabel.IsVisible = true;
        statusLabel.Foreground = Avalonia.Media.Brushes.Gray;
        statusLabel.Text  = "Validating…";
        connectBtn.IsEnabled = false;

        try
        {
            var identity = await _oauth.AuthenticateWithBitbucketAppPasswordAsync(username, appPassword);
            statusLabel.Text = $"Signed in as {identity.Username} ✓";
            await Task.Delay(1000, CancellationToken.None);
            Close(identity);
        }
        catch (Exception ex)
        {
            statusLabel.Foreground = Avalonia.Media.Brushes.OrangeRed;
            statusLabel.Text       = $"Error: {ex.Message}";
            connectBtn.IsEnabled   = true;
        }
    }

    // ── Panel switching ───────────────────────────────────────────────────────

    private void ShowPanel(string which)
    {
        this.FindControl<StackPanel>("InitialPanel")!    .IsVisible = which == "initial";
        this.FindControl<StackPanel>("GitHubFlowPanel")! .IsVisible = which == "github";
        this.FindControl<StackPanel>("BitbucketPanel")!  .IsVisible = which == "bitbucket";

        // Reset Device Flow label when going back
        if (which != "github")
        {
            this.FindControl<TextBlock>("DeviceCodeLabel")!.Text  = string.Empty;
            this.FindControl<TextBlock>("FlowStatusLabel")!.Text  = string.Empty;
            var cancelBtn = this.FindControl<Button>("CancelFlowButton")!;
            cancelBtn.Content    = "Cancel";
            cancelBtn.Foreground = null;
        }
    }
}
