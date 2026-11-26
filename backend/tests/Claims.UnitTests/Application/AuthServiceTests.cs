using Claims.Application.Auth;
using Claims.Domain;
using NSubstitute;

namespace Claims.UnitTests.Application;

public sealed class AuthServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly UserCredentials Officer =
        new(Guid.NewGuid(), "officer1@claimdesk.test", "Priya Menon", Role.Officer, "stored-hash");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordVerifier _passwords = Substitute.For<IPasswordVerifier>();
    private readonly IAccessTokenIssuer _tokens = Substitute.For<IAccessTokenIssuer>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _users.FindCredentialsByEmailAsync(Officer.Email, Arg.Any<CancellationToken>()).Returns(Officer);
        _passwords.Verify("stored-hash", "right").Returns(true);
        _tokens.Issue(Arg.Any<CurrentUser>()).Returns(new AccessToken("jwt", DateTimeOffset.UnixEpoch));
        _service = new AuthService(_users, _passwords, _tokens);
    }

    [Fact]
    public async Task Valid_credentials_return_token_and_user()
    {
        var response = await _service.LoginAsync(new LoginRequest(Officer.Email, "right"), Ct);

        response.ShouldNotBeNull();
        response.AccessToken.ShouldBe("jwt");
        response.ExpiresAt.ShouldBe(DateTimeOffset.UnixEpoch);
        response.User.ShouldBe(new CurrentUser(Officer.Id, Officer.Email, Officer.DisplayName, Role.Officer));
    }

    [Fact]
    public async Task Email_is_matched_case_and_whitespace_insensitively()
    {
        var response = await _service.LoginAsync(new LoginRequest("  Officer1@ClaimDesk.TEST ", "right"), Ct);

        response.ShouldNotBeNull();
    }

    [Fact]
    public async Task Wrong_password_returns_null()
    {
        var response = await _service.LoginAsync(new LoginRequest(Officer.Email, "wrong"), Ct);

        response.ShouldBeNull();
        _tokens.DidNotReceiveWithAnyArgs().Issue(default!);
    }

    [Fact]
    public async Task Unknown_user_returns_null_but_still_runs_a_password_check()
    {
        var response = await _service.LoginAsync(new LoginRequest("nobody@claimdesk.test", "right"), Ct);

        response.ShouldBeNull();
        _passwords.Received(1).Verify(null, "right");
        _tokens.DidNotReceiveWithAnyArgs().Issue(default!);
    }
}
