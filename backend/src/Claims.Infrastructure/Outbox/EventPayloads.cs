using Claims.Domain;

namespace Claims.Infrastructure.Outbox;

// The published contract (payload field of the Kafka envelope, version 1). Kept apart from the domain event records
// so a domain refactor cannot silently change what consumers receive. Envelope fields (eventId, claimId,
// occurredAt, correlationId) are columns on the outbox row and are not repeated here.

internal sealed record MoneyPayload(decimal Amount, string Currency)
{
    public static MoneyPayload From(Money money) => new(money.Amount, money.Currency);
}

internal sealed record ClaimSubmittedPayload(Guid ClaimantId, Guid PolicyId, MoneyPayload ClaimedAmount);

internal sealed record ClaimAssignedPayload(Guid OfficerId);

internal sealed record InformationRequestedPayload(Guid RequestId, Guid OfficerId, string Question);

internal sealed record InformationProvidedPayload(Guid RequestId, Guid ClaimantId);

internal sealed record ClaimAssessedPayload(Guid OfficerId, MoneyPayload Reserve);

internal sealed record ClaimApprovedPayload(Guid OfficerId, MoneyPayload ApprovedAmount);

internal sealed record ClaimRejectedPayload(Guid OfficerId, string Reason);

internal sealed record ClaimSettledPayload(Guid OfficerId, MoneyPayload SettledAmount);
