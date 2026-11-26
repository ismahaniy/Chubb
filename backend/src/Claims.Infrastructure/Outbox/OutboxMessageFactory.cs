using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Domain;

namespace Claims.Infrastructure.Outbox;

internal static class OutboxMessageFactory
{
    public const int Version = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Throws for an event without a payload mapping, so a new event cannot reach the outbox half-defined.</summary>
    public static OutboxMessage Create(IDomainEvent domainEvent, string? correlationId)
    {
        object payload = domainEvent switch
        {
            ClaimSubmitted e => new ClaimSubmittedPayload(e.ClaimantId, e.PolicyId, MoneyPayload.From(e.ClaimedAmount)),
            ClaimAssigned e => new ClaimAssignedPayload(e.OfficerId),
            InformationRequested e => new InformationRequestedPayload(e.RequestId, e.OfficerId, e.Question),
            InformationProvided e => new InformationProvidedPayload(e.RequestId, e.ClaimantId),
            ClaimAssessed e => new ClaimAssessedPayload(e.OfficerId, MoneyPayload.From(e.Reserve)),
            ClaimApproved e => new ClaimApprovedPayload(e.OfficerId, MoneyPayload.From(e.ApprovedAmount)),
            ClaimRejected e => new ClaimRejectedPayload(e.OfficerId, e.Reason),
            ClaimSettled e => new ClaimSettledPayload(e.OfficerId, MoneyPayload.From(e.SettledAmount)),
            _ => throw new InvalidOperationException($"No outbox payload is defined for {domainEvent.GetType().Name}."),
        };

        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            EventType = domainEvent.GetType().Name,
            Version = Version,
            ClaimId = domainEvent.ClaimId,
            CorrelationId = correlationId,
            Payload = JsonSerializer.Serialize(payload, payload.GetType(), Json),
            OccurredAt = domainEvent.OccurredAt,
        };
    }
}
