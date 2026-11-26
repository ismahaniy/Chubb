using Claims.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.Auth;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Issue(CurrentUser user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.LifetimeMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [TokenClaims.Subject] = user.Id.ToString(),
                [TokenClaims.Email] = user.Email,
                [TokenClaims.Name] = user.DisplayName,
                [TokenClaims.Role] = user.Role.ToString(),
            },
        });

        return new AccessToken(token, expiresAt);
    }
}
