using Claims.Application.Common;
using Claims.Domain;
using Claims.Infrastructure.Persistence;

namespace Claims.Infrastructure.Outbox;

/// <summary>Adds outbox rows to the request's DbContext, so they commit (or roll back) with the business change.</summary>
internal sealed class EfOutbox(ClaimsDbContext db, ICorrelationContext correlation) : IOutbox
{
    public void Enqueue(Claim claim)
    {
        foreach (var domainEvent in claim.DomainEvents)
        {
            db.OutboxMessages.Add(OutboxMessageFactory.Create(domainEvent, correlation.CorrelationId));
        }

        claim.ClearDomainEvents();
    }
}
