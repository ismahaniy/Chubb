using Claims.Application.Claims;
using Claims.Application.Reports;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Reports;

internal sealed class ReportRepository(ClaimsDbContext db) : IReportRepository
{
    private static readonly TimeSpan SettlementWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// One SQL GROUP BY. Currency is part of the key and is taken from the same Money as the amount, so the database
    /// never adds two currencies together.
    /// </summary>
    public async Task<IReadOnlyList<ExposureLine>> GetExposureAsync(CancellationToken cancellationToken) =>
        await db.Claims
            .AsNoTracking()
            .Where(c => !Claim.ClosedStatuses.Contains(c.Status))
            .Select(c => new
            {
                c.Market,
                c.ProductType,
                Currency = c.Reserve != null ? c.Reserve.Currency : c.ClaimedAmount.Currency,
                Amount = c.Reserve != null ? c.Reserve.Amount : c.ClaimedAmount.Amount,
            })
            .GroupBy(x => new { x.Market, x.ProductType, x.Currency })
            .OrderBy(g => g.Key.Market)
            .ThenBy(g => g.Key.ProductType)
            .ThenBy(g => g.Key.Currency)
            .Select(g => new ExposureLine(
                g.Key.Market,
                g.Key.ProductType,
                g.Count(),
                new MoneyDto(g.Sum(x => x.Amount), g.Key.Currency)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OfficerWorkload>> GetWorkloadAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settledSince = now - SettlementWindow;

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Role == Role.Officer)
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Id)
            .Select(u => new OfficerWorkload(
                u.Id,
                u.DisplayName,
                db.Claims.Count(c => c.AssignedOfficerId == u.Id && !Claim.ClosedStatuses.Contains(c.Status)),
                // Same rule as the queue's SlaBreached: still awaiting a decision and past the due date.
                db.Claims.Count(c => c.AssignedOfficerId == u.Id && Claim.OpenStatuses.Contains(c.Status) && c.SlaDueAt < now),
                // Settled is final, so each settled claim has exactly one such row; ChangedBy is who settled it.
                db.ClaimStatusHistory.Count(h =>
                    h.ToStatus == ClaimStatus.Settled && h.ChangedBy == u.Id && h.ChangedAt >= settledSince)))
            .ToListAsync(cancellationToken);
    }
}
