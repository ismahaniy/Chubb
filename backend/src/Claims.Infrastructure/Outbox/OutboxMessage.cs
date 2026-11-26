namespace Claims.Infrastructure.Outbox;

/// <summary>
/// A domain event waiting to be published to Kafka. Saved in the same transaction as the business change;
/// the Worker publishes unsent rows and sets <see cref="SentAt"/>.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>The event id; becomes eventId in the Kafka envelope.</summary>
    public Guid Id { get; init; }

    public required string EventType { get; init; }
    public int Version { get; init; } = 1;
    public Guid ClaimId { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>The event payload as JSON (jsonb).</summary>
    public required string Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
