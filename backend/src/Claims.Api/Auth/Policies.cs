using Claims.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Claims.Api.Auth;

public static class Policies
{
    public const string Claimant = nameof(Role.Claimant);
    public const string Officer = nameof(Role.Officer);
    public const string Manager = nameof(Role.Manager);

    /// <summary>Officers and managers: anyone who works claims rather than files them.</summary>
    public const string Staff = "Staff";

    /// <summary>Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].</summary>
    public static AuthorizationBuilder AddClaimsPolicies(this AuthorizationBuilder builder) => builder
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy(Claimant, p => p.RequireRole(Claimant))
        .AddPolicy(Officer, p => p.RequireRole(Officer))
        .AddPolicy(Manager, p => p.RequireRole(Manager))
        .AddPolicy(Staff, p => p.RequireRole(Officer, Manager));
}
