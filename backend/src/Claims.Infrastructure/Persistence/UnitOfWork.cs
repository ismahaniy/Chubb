using Claims.Application.Common;

namespace Claims.Infrastructure.Persistence;

internal sealed class UnitOfWork(ClaimsDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
