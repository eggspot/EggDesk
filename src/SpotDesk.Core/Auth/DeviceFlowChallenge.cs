namespace SpotDesk.Core.Auth;

/// <summary>
/// Returned by <see cref="IOAuthService.StartGitHubDeviceFlowAsync"/> and consumed by
/// <see cref="IOAuthService.PollGitHubDeviceFlowAsync"/>.
/// Show <see cref="UserCode"/> and <see cref="VerificationUri"/> to the user.
/// </summary>
public record DeviceFlowChallenge
{
    /// <summary>Opaque code used when polling for the token. Never show to user.</summary>
    public string DeviceCode      { get; init; } = string.Empty;

    /// <summary>Short code the user types at <see cref="VerificationUri"/>. e.g. "WDJB-MJHT"</summary>
    public string UserCode        { get; init; } = string.Empty;

    /// <summary>URL the user opens in their browser. Always https://github.com/login/device</summary>
    public string VerificationUri { get; init; } = string.Empty;

    /// <summary>Seconds until the device code expires.</summary>
    public int ExpiresIn          { get; init; } = 900;

    /// <summary>Minimum seconds to wait between poll requests.</summary>
    public int Interval           { get; init; } = 5;

    /// <summary>OAuth App client ID used to start this flow — needed when polling.</summary>
    public string ClientId        { get; init; } = string.Empty;
}
