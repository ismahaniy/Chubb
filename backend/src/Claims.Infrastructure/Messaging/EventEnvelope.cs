using System.Text.Json;
using Claims.Infrastructure.Outbox;

namespace Claims.Infrastructure.Messaging;

/// <summary>
/// The Kafka message value on claims.events: envelope fields plus the versioned payload as a nested JSON object.
/// </summary>
public sealed record EventEnvelope(
    Guid EventId,
    string EventType,
    int Version,
    DateTimeOffset OccurredAt,
    Guid ClaimId,
    string? CorrelationId,
    JsonElement Payload)
{
    public static EventEnvelope From(OutboxMessage message) => new(
        message.Id,
        message.EventType,
        message.Version,
        message.OccurredAt,
        message.ClaimId,
        message.CorrelationId,
        // Deserialize (not JsonDocument.Parse) gives an element that owns its memory, so nothing needs disposing.
        JsonSerializer.Deserialize<JsonElement>(message.Payload));

    public string ToJson() => JsonSerializer.Serialize(this, OutboxMessageFactory.Json);

    /// <summary>Throws <see cref="JsonException"/> for a malformed message, which the consumer retries and then dead-letters.</summary>
    public static EventEnvelope Parse(string json)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, OutboxMessageFactory.Json)
            ?? throw new JsonException("The message is JSON null.");

        if (envelope.EventId == Guid.Empty || envelope.ClaimId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType)
            || envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The envelope is missing eventId, claimId, eventType or an object payload.");
        }

        return envelope;
    }

    /// <summary>The payload as one of the published payload records.</summary>
    internal T PayloadAs<T>() =>
        Payload.Deserialize<T>(OutboxMessageFactory.Json) ?? throw new JsonException($"The {EventType} payload is JSON null.");
}
