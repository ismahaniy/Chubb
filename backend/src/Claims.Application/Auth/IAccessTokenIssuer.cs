namespace Claims.Application.Auth;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(CurrentUser user);
}
