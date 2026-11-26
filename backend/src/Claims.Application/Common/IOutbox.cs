using Claims.Domain;

namespace Claims.Application.Common;

public interface IOutbox
{
    /// <summary>
    /// Stages the claim's pending domain events as outbox rows in the current unit of work and clears them from the
    /// claim, so they are saved by the same SaveChanges as the claim itself and can never be staged twice.
    /// </summary>
    void Enqueue(Claim claim);
}
