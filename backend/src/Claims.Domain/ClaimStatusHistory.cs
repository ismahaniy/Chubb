namespace Claims.Domain;

public sealed class ClaimStatusHistory
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }

    /// <summary>Null for the initial submission.</summary>
    public ClaimStatus? FromStatus { get; private set; }

    public ClaimStatus ToStatus { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private ClaimStatusHistory()
    {
    }

    internal ClaimStatusHistory(Guid claimId, ClaimStatus? from, ClaimStatus to, Guid changedBy, DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7();
        ClaimId = claimId;
        FromStatus = from;
        ToStatus = to;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }
}
