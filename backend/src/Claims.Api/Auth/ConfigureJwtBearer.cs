using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Claims.Api.Auth;

/// <summary>
/// Reads JwtOptions when the bearer handler is first used, not at registration, so tooling that builds the app
/// without starting it (build-time OpenAPI generation) does not need a signing key.
/// </summary>
internal sealed class ConfigureJwtBearer(IOptions<JwtOptions> jwtOptions) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.CreateSigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = TokenClaims.Name,
            RoleClaimType = TokenClaims.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }
}
