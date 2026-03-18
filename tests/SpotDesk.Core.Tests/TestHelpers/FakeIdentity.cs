using SpotDesk.Core.Auth;

namespace SpotDesk.Core.Tests.TestHelpers;

public static class FakeIdentity
{
    public static GitHubIdentity GitHub(long userId = 99999L, string login = "testuser") =>
        new(userId, login, "ghp_fake_token_for_testing");
}
