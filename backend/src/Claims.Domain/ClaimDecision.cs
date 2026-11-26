namespace Claims.Domain;

public sealed class ClaimDecision
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public DecisionOutcome Outcome { get; private set; }

    /// <summary>Set only when the outcome is Approved.</summary>
    public Money? ApprovedAmount { get; private set; }

    public string Reason { get; private set; } = null!;
    public Guid DecidedBy { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    private ClaimDecision()
    {
    }

    internal ClaimDecision(
        Guid claimId,
        DecisionOutcome outcome,
        Money? approvedAmount,
        string reason,
        Guid decidedBy,
        DateTimeOffset decidedAt)
    {
        Id = Guid.CreateVersion7();
        ClaimId = claimId;
        Outcome = outcome;
        ApprovedAmount = approvedAmount;
        Reason = reason;
        DecidedBy = decidedBy;
        DecidedAt = decidedAt;
    }
}
