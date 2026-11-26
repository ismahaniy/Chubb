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
}
