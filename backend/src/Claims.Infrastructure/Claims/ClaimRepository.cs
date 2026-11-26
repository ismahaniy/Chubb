using Claims.Application.Claims;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Claims;

internal sealed class ClaimRepository(ClaimsDbContext db) : IClaimRepository
{
    public Task<string> NextClaimNumberAsync(CancellationToken cancellationToken) =>
        ClaimNumberSequence.NextAsync(db, cancellationToken);

    public void Add(Claim claim) => db.Claims.Add(claim);

    public Task<Claim?> GetForUpdateAsync(Guid claimId, CancellationToken cancellationToken) =>
        db.Claims
            .Include(c => c.StatusHistory)
            .Include(c => c.InformationRequests)
            .Include(c => c.Decision)
            .AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == claimId, cancellationToken);

    public async Task<IReadOnlyList<ClaimSummary>> ListSummariesByClaimantAsync(Guid claimantId, CancellationToken cancellationToken) =>
        await db.Claims
            .AsNoTracking()
            .Where(c => c.ClaimantId == claimantId)
            .Join(db.Policies, c => c.PolicyId, p => p.Id, (c, p) => new { Claim = c, p.PolicyNumber })
            .OrderByDescending(x => x.Claim.SubmittedAt)
            .Select(x => new ClaimSummary(
                x.Claim.Id,
                x.Claim.ClaimNumber,
                x.PolicyNumber,
                x.Claim.ProductType,
                x.Claim.Market,
                x.Claim.Status,
                new MoneyDto(x.Claim.ClaimedAmount.Amount, x.Claim.ClaimedAmount.Currency),
                x.Claim.SubmittedAt))
            .ToListAsync(cancellationToken);

    public Task<ClaimDetail?> GetDetailAsync(Guid claimId, CancellationToken cancellationToken) =>
        db.Claims
            .AsNoTracking()
            .Where(c => c.Id == claimId)
            .Join(db.Policies, c => c.PolicyId, p => p.Id, (c, p) => new ClaimDetail(
                c.Id,
                c.ClaimNumber,
                p.PolicyNumber,
                c.ClaimantId,
                c.ProductType,
                c.Market,
                c.Status,
                c.IncidentDate,
                c.Description,
                new MoneyDto(c.ClaimedAmount.Amount, c.ClaimedAmount.Currency),
                new MoneyDto(c.MaxPayable.Amount, c.MaxPayable.Currency),
                c.SubmittedAt,
                c.Decision == null
                    ? null
                    : new DecisionDto(
                        c.Decision.Outcome,
                        c.Decision.ApprovedAmount == null
                            ? null
                            : new MoneyDto(c.Decision.ApprovedAmount.Amount, c.Decision.ApprovedAmount.Currency),
                        c.Decision.Reason,
                        c.Decision.DecidedAt),
                c.StatusHistory
                    .OrderBy(h => h.ChangedAt)
                    .ThenBy(h => h.Id) // v7 ids are time-ordered: a stable tie-break for equal timestamps
                    .Select(h => new StatusChangeDto(h.FromStatus, h.ToStatus, h.ChangedAt))
                    .ToList(),
                c.InformationRequests
                    .Where(r => r.AnsweredAt == null)
                    .OrderBy(r => r.RequestedAt)
                    .Select(r => new InformationRequestDto(r.Id, r.Question, r.RequestedAt))
                    .ToList()))
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
}
