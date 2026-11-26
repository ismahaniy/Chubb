namespace Claims.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }
    Guid ClaimId { get; }
    DateTimeOffset OccurredAt { get; }
}

public abstract record ClaimEvent(Guid ClaimId, DateTimeOffset OccurredAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
}

public sealed record ClaimSubmitted(Guid ClaimId, DateTimeOffset OccurredAt, Guid ClaimantId, Guid PolicyId, Money ClaimedAmount)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record ClaimAssigned(Guid ClaimId, DateTimeOffset OccurredAt, Guid OfficerId)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record InformationRequested(Guid ClaimId, DateTimeOffset OccurredAt, Guid RequestId, Guid OfficerId, string Question)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record InformationProvided(Guid ClaimId, DateTimeOffset OccurredAt, Guid RequestId, Guid ClaimantId)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record ClaimAssessed(Guid ClaimId, DateTimeOffset OccurredAt, Guid OfficerId, Money Reserve)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record ClaimApproved(Guid ClaimId, DateTimeOffset OccurredAt, Guid OfficerId, Money ApprovedAmount)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record ClaimRejected(Guid ClaimId, DateTimeOffset OccurredAt, Guid OfficerId, string Reason)
    : ClaimEvent(ClaimId, OccurredAt);

public sealed record ClaimSettled(Guid ClaimId, DateTimeOffset OccurredAt, Guid OfficerId, Money SettledAmount)
    : ClaimEvent(ClaimId, OccurredAt);
