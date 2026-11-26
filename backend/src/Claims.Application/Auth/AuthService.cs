namespace Claims.Application.Auth;

public sealed class AuthService(IUserRepository users, IPasswordVerifier passwords, IAccessTokenIssuer tokens)
{
    /// <returns>null when the email is unknown or the password is wrong; callers cannot tell which.</returns>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var credentials = await users.FindCredentialsByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);

        // Always verify, even for an unknown user, so response time does not reveal which emails exist.
        if (!passwords.Verify(credentials?.PasswordHash, request.Password) || credentials is null)
        {
            return null;
        }

        var user = new CurrentUser(credentials.Id, credentials.Email, credentials.DisplayName, credentials.Role);
        var token = tokens.Issue(user);
        return new LoginResponse(token.Token, token.ExpiresAt, user);
    }
}
