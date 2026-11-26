using Claims.Api.Auth;
using Claims.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize(Policies.Claimant)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class PoliciesController(ClaimantClaimService claimantClaims) : ControllerBase
{
    /// <summary>The signed-in claimant's policies, to submit a claim against. A claim must match the policy's product and currency.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<PolicySummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PolicySummary>>> Mine(CancellationToken cancellationToken) =>
        Ok(await claimantClaims.ListMyPoliciesAsync(User.ToActor(), cancellationToken));
}
