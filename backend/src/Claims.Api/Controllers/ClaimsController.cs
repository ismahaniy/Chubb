using Claims.Api.Auth;
using Claims.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/claims")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ClaimsController(ClaimantClaimService claimantClaims) : ControllerBase
{
    /// <summary>Submit a claim on one of the signed-in claimant's policies.</summary>
    [HttpPost]
    [Authorize(Policies.Claimant)]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> Submit(SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await claimantClaims.SubmitAsync(User.ToActor(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, claim);
    }

    /// <summary>The signed-in claimant's claims, newest first.</summary>
    [HttpGet("mine")]
    [Authorize(Policies.Claimant)]
    [ProducesResponseType<IReadOnlyList<ClaimSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClaimSummary>>> Mine(CancellationToken cancellationToken) =>
        Ok(await claimantClaims.ListMineAsync(User.ToActor(), cancellationToken));

    /// <summary>One claim with its status timeline and open information requests. Claimants see only their own.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> Get(Guid id, CancellationToken cancellationToken) =>
        await claimantClaims.GetAsync(User.ToActor(), id, cancellationToken);

    /// <summary>Answer the open information request; the claim goes back to UnderReview.</summary>
    [HttpPost("{id:guid}/information-requests/{requestId:guid}/response")]
    [Authorize(Policies.Claimant)]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> RespondToInformationRequest(
        Guid id,
        Guid requestId,
        InformationResponseRequest request,
        CancellationToken cancellationToken) =>
        await claimantClaims.RespondToInformationRequestAsync(User.ToActor(), id, requestId, request, cancellationToken);
}
