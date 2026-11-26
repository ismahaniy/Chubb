using Claims.Application.Common;
using Claims.Domain;

namespace Claims.Application.Claims;

/// <summary>
/// The officer use cases. Any officer may pick up a submitted claim; after that only the assigned officer may work it.
/// Business rules (allowed transitions, reserve before decision, maximum payable) live in the Claim aggregate.
/// </summary>
public sealed class OfficerClaimService(
    IClaimRepository claims,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <summary>
    /// Two officers racing for the same claim: the loser either sees it already UnderReview (DomainException) or
    /// loses the xmin check on save (DbUpdateConcurrencyException). Both map to 409.
    /// </summary>
    public Task<ClaimDetail> AssignAsync(Actor actor, Guid claimId, CancellationToken cancellationToken) =>
        ChangeAsync(actor, claimId, assignedOnly: false, (claim, now) => claim.Assign(actor.UserId, now), cancellationToken);

    public Task<ClaimDetail> RequestInformationAsync(
        Actor actor, Guid claimId, InformationRequestRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(actor, claimId, assignedOnly: true,
            (claim, now) => claim.RequestInformation(actor.UserId, request.Question, now), cancellationToken);

    public Task<ClaimDetail> RecordAssessmentAsync(
        Actor actor, Guid claimId, AssessmentRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(actor, claimId, assignedOnly: true,
            (claim, now) => claim.RecordAssessment(
                actor.UserId, new Money(request.ReserveAmount!.Value, request.Currency), request.Notes, now),
            cancellationToken);

    public Task<ClaimDetail> DecideAsync(Actor actor, Guid claimId, DecisionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(actor, claimId, assignedOnly: true, (claim, now) =>
        {
            // DecisionRequest.Validate guarantees amount and currency are present for an approval.
            if (request.Outcome == DecisionOutcome.Approved)
            {
                claim.Approve(actor.UserId, new Money(request.ApprovedAmount!.Value, request.Currency!), request.Reason, now);
            }
            else
            {
                claim.Reject(actor.UserId, request.Reason, now);
            }
        }, cancellationToken);

    public Task<ClaimDetail> SettleAsync(Actor actor, Guid claimId, CancellationToken cancellationToken) =>
        ChangeAsync(actor, claimId, assignedOnly: true, (claim, now) => claim.Settle(actor.UserId, now), cancellationToken);

    /// <summary>
    /// Load, authorize, apply one aggregate command, then save the claim, its new status history and its outbox rows
    /// in a single SaveChanges (one transaction).
    /// </summary>
    private async Task<ClaimDetail> ChangeAsync(
        Actor actor,
        Guid claimId,
        bool assignedOnly,
        Action<Claim, DateTimeOffset> change,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetForUpdateAsync(claimId, cancellationToken)
            ?? throw new NotFoundException($"Claim {claimId} was not found.");

        if (assignedOnly && claim.AssignedOfficerId != actor.UserId)
        {
            throw new ForbiddenException($"Only the officer assigned to claim {claim.ClaimNumber} can work on it.");
        }

        change(claim, clock.GetUtcNow());

        outbox.Enqueue(claim);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await claims.GetDetailAsync(claim.Id, cancellationToken)
            ?? throw new NotFoundException($"Claim {claimId} was not found.");
    }
}
