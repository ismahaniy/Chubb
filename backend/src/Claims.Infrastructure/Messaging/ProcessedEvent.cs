namespace Claims.Infrastructure.Messaging;

/// <summary>
/// Marks an event as handled by one consumer. Inserted in the same transaction as the consumer's work,
/// so a redelivered event is skipped. Keyed by (event, consumer) so several consumers can each handle it once.
/// </summary>
public sealed class ProcessedEvent
{
    public Guid EventId { get; init; }
    public required string Consumer { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}
