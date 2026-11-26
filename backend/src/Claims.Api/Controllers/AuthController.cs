using System.Security.Claims;
using Claims.Api.Auth;
using Claims.Application.Auth;
using Claims.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    /// <summary>Exchange demo credentials for a bearer token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return response is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
            : response;
    }

    /// <summary>The signed-in user, as read from the token.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public ActionResult<CurrentUser> Me() => new CurrentUser(
        Guid.Parse(User.FindFirstValue(TokenClaims.Subject)!),
        User.FindFirstValue(TokenClaims.Email)!,
        User.FindFirstValue(TokenClaims.Name)!,
        Enum.Parse<Role>(User.FindFirstValue(TokenClaims.Role)!));
}
