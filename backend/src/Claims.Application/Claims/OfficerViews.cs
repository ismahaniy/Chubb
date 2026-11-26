using Claims.Domain;

namespace Claims.Application.Claims;

public enum QueueScope
{
    /// <summary>Submitted claims nobody has picked up yet.</summary>
    Unassigned,

    /// <summary>The signed-in officer's claims that still need work: under review, awaiting information, or approved and not yet settled.</summary>
    Mine,
}

/// <param name="SlaBreached">The claim is still open (not yet decided) and past <paramref name="SlaDueAt"/>.</param>
public sealed record QueueItem(
    Guid Id,
    string ClaimNumber,
    string PolicyNumber,
    Market Market,
    ProductType ProductType,
    ClaimStatus Status,
    MoneyDto ClaimedAmount,
    Guid? AssignedOfficerId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset SlaDueAt,
    bool SlaBreached);

/// <summary>One page of the queue, ordered by SLA due date (most urgent first).</summary>
public sealed record QueuePage(IReadOnlyList<QueueItem> Items, int Page, int PageSize, int TotalCount);
