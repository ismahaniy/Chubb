using Claims.Application.Common;
using Claims.Domain;

namespace Claims.Application.Claims;

/// <summary>
/// The claimant use cases. Ownership is enforced here, not only by controller roles: a claimant only ever sees
/// or changes their own claims. Business rules live in the Claim aggregate.
/// </summary>
public sealed class ClaimantClaimService(
    IPolicyRepository policies,
    IClaimRepository claims,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<ClaimDetail> SubmitAsync(Actor actor, SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var policyNumber = request.PolicyNumber.Trim();
        var policy = await policies.FindByNumberAsync(policyNumber, cancellationToken);

        // Someone else's policy looks exactly like a missing one, so policy numbers cannot be probed.
        if (policy is null || policy.HolderId != actor.UserId)
        {
            throw new NotFoundException($"Policy {policyNumber} was not found.");
        }

        var claim = Claim.Submit(
            await claims.NextClaimNumberAsync(cancellationToken),
            policy,
            actor.UserId,
            request.ProductType!.Value,
            request.IncidentDate!.Value,
            request.Description,
            new Money(request.ClaimedAmount!.Value, request.Currency),
            clock.GetUtcNow());

        claims.Add(claim);
        outbox.Enqueue(claim);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReadDetailAsync(claim.Id, cancellationToken);
    }

    /// <summary>Always the actor's own policies: the holder comes from the token, never from the request.</summary>
    public Task<IReadOnlyList<PolicySummary>> ListMyPoliciesAsync(Actor actor, CancellationToken cancellationToken) =>
        policies.ListForHolderAsync(actor.UserId, cancellationToken);

    public Task<IReadOnlyList<ClaimSummary>> ListMineAsync(Actor actor, CancellationToken cancellationToken) =>
        claims.ListSummariesByClaimantAsync(actor.UserId, cancellationToken);

    /// <summary>Staff may read any claim; a claimant only their own.</summary>
    public async Task<ClaimDetail> GetAsync(Actor actor, Guid claimId, CancellationToken cancellationToken)
    {
        var detail = await ReadDetailAsync(claimId, cancellationToken);
        if (actor.Role == Role.Claimant && detail.ClaimantId != actor.UserId)
        {
            throw new ForbiddenException("You can only view your own claims.");
        }

        return detail;
    }

    public async Task<ClaimDetail> RespondToInformationRequestAsync(
        Actor actor,
        Guid claimId,
        Guid requestId,
        InformationResponseRequest request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.GetForUpdateAsync(claimId, cancellationToken)
            ?? throw new NotFoundException($"Claim {claimId} was not found.");

        if (claim.ClaimantId != actor.UserId)
        {
            throw new ForbiddenException("Only the claimant can answer information requests on this claim.");
        }

        if (claim.InformationRequests.All(r => r.Id != requestId))
        {
            throw new NotFoundException($"Information request {requestId} was not found on claim {claim.ClaimNumber}.");
        }

        claim.ProvideInformation(requestId, actor.UserId, request.Answer, clock.GetUtcNow());

        outbox.Enqueue(claim);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReadDetailAsync(claim.Id, cancellationToken);
    }

    private async Task<ClaimDetail> ReadDetailAsync(Guid claimId, CancellationToken cancellationToken) =>
        await claims.GetDetailAsync(claimId, cancellationToken)
        ?? throw new NotFoundException($"Claim {claimId} was not found.");
}
