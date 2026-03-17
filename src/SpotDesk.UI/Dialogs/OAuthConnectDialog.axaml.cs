using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SpotDesk.Core.Auth;

namespace SpotDesk.UI.Dialogs;

public partial class OAuthConnectDialog : Window
{
    private readonly IOAuthService _oauth;

    public OAuthConnectDialog() : this(AppServices.GetRequired<IOAuthService>()) { }

    public OAuthConnectDialog(IOAuthService oauth)
    {
        InitializeComponent();
        _oauth = oauth;

        this.FindControl<Button>("GitHubButton")!.Click        += (_, _) => _ = AuthenticateAsync(OAuthProvider.GitHub);
        this.FindControl<Button>("BitbucketButton")!.Click     += (_, _) => _ = AuthenticateAsync(OAuthProvider.Bitbucket);
        this.FindControl<Button>("MasterPasswordButton")!.Click += (_, _) => Close(null);
    }

    private async Task AuthenticateAsync(OAuthProvider provider)
    {
        var status = this.FindControl<TextBlock>("StatusLabel")!;
        var githubBtn    = this.FindControl<Button>("GitHubButton")!;
        var bitbucketBtn = this.FindControl<Button>("BitbucketButton")!;

        githubBtn.IsEnabled    = false;
        bitbucketBtn.IsEnabled = false;
        status.IsVisible       = true;
        status.Text            = provider == OAuthProvider.GitHub
            ? "Opening browser… complete sign-in there."
            : "Opening browser… complete sign-in there.";

        try
        {
            if (provider == OAuthProvider.GitHub)
            {
                var identity = await _oauth.AuthenticateGitHubAsync();
                status.Text = $"Signed in as {identity.Login}";
                await Task.Delay(1200);
                Close(identity);
            }
            else
            {
                var identity = await _oauth.AuthenticateBitbucketAsync();
                status.Text = $"Signed in as {identity.Username}";
                await Task.Delay(1200);
                Close(identity);
            }
        }
        catch (Exception ex)
        {
            status.Foreground = Avalonia.Media.Brushes.OrangeRed;
            status.Text       = $"Sign-in failed: {ex.Message}";
            githubBtn.IsEnabled    = true;
            bitbucketBtn.IsEnabled = true;
        }
    }
}
