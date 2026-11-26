using Claims.Domain;

namespace Claims.UnitTests.Domain;

/// <summary>Builds valid policies and claims, driving claims into a state through the public API only.</summary>
internal static class ClaimTestData
{
    public const string Currency = "SGD";

    public static readonly Guid ClaimantId = Guid.CreateVersion7();
    public static readonly Guid OfficerId = Guid.CreateVersion7();
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly IncidentDate = new(2026, 2, 15);

    // Coverage 10,000.00 - deductible 500.00 => max payable 9,500.00
    public static Policy Policy(Guid? holderId = null) => new(
        "POL-SG-0001",
        holderId ?? ClaimantId,
        Market.Singapore,
        ProductType.Motor,
        Money(10_000m),
        Money(500m),
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31));

    public static Money Money(decimal amount) => new(amount, Currency);

    public static Claim SubmittedClaim() =>
        Claim.Submit(Policy(), ClaimantId, IncidentDate, "Rear-ended at a junction", Money(3_000m), Now);

    /// <summary>
    /// A claim in <paramref name="status"/> reached by the legal path. Claims at UnderReview or beyond
    /// already have a reserve, so a decision from UnderReview can only fail on status.
    /// </summary>
    public static Claim ClaimIn(ClaimStatus status)
    {
        var claim = SubmittedClaim();
        if (status != ClaimStatus.Submitted)
        {
            claim.Assign(OfficerId, Now);
            claim.RecordAssessment(OfficerId, Money(2_500m), "Workshop quote received", Now);
        }

        switch (status)
        {
            case ClaimStatus.Submitted:
            case ClaimStatus.UnderReview:
                break;
            case ClaimStatus.AwaitingInformation:
                claim.RequestInformation(OfficerId, "Please upload the police report.", Now);
                break;
            case ClaimStatus.Approved:
                claim.Approve(OfficerId, Money(2_500m), "Covered loss", Now);
                break;
            case ClaimStatus.Rejected:
                claim.Reject(OfficerId, "Excluded peril", Now);
                break;
            case ClaimStatus.Settled:
                claim.Approve(OfficerId, Money(2_500m), "Covered loss", Now);
                claim.Settle(OfficerId, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        claim.Status.ShouldBe(status);
        claim.ClearDomainEvents();
        return claim;
    }
}
