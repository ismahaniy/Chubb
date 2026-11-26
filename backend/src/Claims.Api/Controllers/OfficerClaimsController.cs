using Claims.Api.Auth;
using Claims.Application.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

/// <summary>Officer claim transitions. Only the assigned officer may act on a claim after assignment.</summary>
[ApiController]
[Route("api/claims")]
[Authorize(Policies.Officer)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OfficerClaimsController(OfficerClaimService officerClaims) : ControllerBase
{
    /// <summary>Pick up a submitted claim; it moves to UnderReview. 409 if another officer already took it.</summary>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> Assign(Guid id, CancellationToken cancellationToken) =>
        await officerClaims.AssignAsync(User.ToActor(), id, cancellationToken);

    /// <summary>Ask the claimant a question; the claim moves to AwaitingInformation.</summary>
    [HttpPost("{id:guid}/information-requests")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> RequestInformation(
        Guid id, InformationRequestRequest request, CancellationToken cancellationToken) =>
        await officerClaims.RequestInformationAsync(User.ToActor(), id, request, cancellationToken);

    /// <summary>Record or update the reserve and assessment notes. The status does not change.</summary>
    [HttpPost("{id:guid}/assessment")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> RecordAssessment(
        Guid id, AssessmentRequest request, CancellationToken cancellationToken) =>
        await officerClaims.RecordAssessmentAsync(User.ToActor(), id, request, cancellationToken);

    /// <summary>Approve with an amount (up to the maximum payable) or reject. Needs a recorded reserve.</summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> Decide(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        await officerClaims.DecideAsync(User.ToActor(), id, request, cancellationToken);

    /// <summary>Settle an approved claim for its approved amount.</summary>
    [HttpPost("{id:guid}/settle")]
    [ProducesResponseType<ClaimDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClaimDetail>> Settle(Guid id, CancellationToken cancellationToken) =>
        await officerClaims.SettleAsync(User.ToActor(), id, cancellationToken);
}
