using System.Security.Claims;
using Claims.Application.Common;
using Claims.Domain;

namespace Claims.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Only call on an authenticated principal: our tokens always carry sub and role.</summary>
    public static Actor ToActor(this ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(TokenClaims.Subject)!),
        Enum.Parse<Role>(user.FindFirstValue(TokenClaims.Role)!));
}
