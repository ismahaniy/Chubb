using Claims.Application.Claims;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Claims;

internal sealed class ClaimRepository(ClaimsDbContext db) : IClaimRepository
{
    // An officer's own claims that still need work. Approved stays here because it still has to be settled.
    private static readonly ClaimStatus[] MineStatuses = [ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation, ClaimStatus.Approved];

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

    public async Task<QueuePage> GetQueueAsync(QueueQuery query, Guid officerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claims = query.Scope switch
        {
            QueueScope.Unassigned => db.Claims.Where(c => c.AssignedOfficerId == null),
            QueueScope.Mine => db.Claims.Where(c => c.AssignedOfficerId == officerId && MineStatuses.Contains(c.Status)),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Scope, null),
        };

        if (query.Market is { } market)
        {
            claims = claims.Where(c => c.Market == market);
        }

        if (query.Status is { } status)
        {
            claims = claims.Where(c => c.Status == status);
        }

        claims = claims.AsNoTracking();
        var total = await claims.CountAsync(cancellationToken);
        var items = await claims
            .Join(db.Policies, c => c.PolicyId, p => p.Id, (c, p) => new { Claim = c, p.PolicyNumber })
            .OrderBy(x => x.Claim.SlaDueAt)
            .ThenBy(x => x.Claim.Id) // v7 ids: a stable tie-break, so pages never overlap
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new QueueItem(
                x.Claim.Id,
                x.Claim.ClaimNumber,
                x.PolicyNumber,
                x.Claim.Market,
                x.Claim.ProductType,
                x.Claim.Status,
                new MoneyDto(x.Claim.ClaimedAmount.Amount, x.Claim.ClaimedAmount.Currency),
                x.Claim.AssignedOfficerId,
                x.Claim.SubmittedAt,
                x.Claim.SlaDueAt,
                x.Claim.SlaDueAt < now && Claim.OpenStatuses.Contains(x.Claim.Status)))
            .ToListAsync(cancellationToken);

        return new QueuePage(items, query.Page, query.PageSize, total);
    }
}
