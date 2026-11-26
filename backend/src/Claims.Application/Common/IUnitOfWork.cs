namespace Claims.Application.Common;

/// <summary>Commits every change staged in the current scope (aggregates and outbox rows) in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
