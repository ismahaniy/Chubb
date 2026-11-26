using Claims.Domain;

namespace Claims.Application.Claims;

public interface IClaimRepository
{
    Task<string> NextClaimNumberAsync(CancellationToken cancellationToken);

    void Add(Claim claim);

    /// <summary>The tracked aggregate with its information requests and status history, ready to change and save.</summary>
    Task<Claim?> GetForUpdateAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ClaimSummary>> ListSummariesByClaimantAsync(Guid claimantId, CancellationToken cancellationToken);

    Task<ClaimDetail?> GetDetailAsync(Guid claimId, CancellationToken cancellationToken);
}
