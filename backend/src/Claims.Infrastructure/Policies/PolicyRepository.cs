using Claims.Application.Claims;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Policies;

internal sealed class PolicyRepository(ClaimsDbContext db) : IPolicyRepository
{
    public Task<Policy?> FindByNumberAsync(string policyNumber, CancellationToken cancellationToken) =>
        db.Policies
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PolicyNumber == policyNumber, cancellationToken);

    /// <summary>
    /// A holder has a handful of policies, so they are loaded whole and mapped in memory: Currency and MaxPayable then
    /// come from the domain's own properties rather than a copy of their rules in SQL.
    /// </summary>
    public async Task<IReadOnlyList<PolicySummary>> ListForHolderAsync(Guid holderId, CancellationToken cancellationToken)
    {
        var policies = await db.Policies
            .AsNoTracking()
            .Where(p => p.HolderId == holderId)
            .OrderBy(p => p.PolicyNumber)
            .ToListAsync(cancellationToken);

        return policies
            .Select(p => new PolicySummary(
                p.PolicyNumber,
                p.Market,
                p.ProductType,
                p.Currency,
                new MoneyDto(p.MaxPayable.Amount, p.MaxPayable.Currency),
                p.StartDate,
                p.EndDate))
            .ToList();
    }
}
