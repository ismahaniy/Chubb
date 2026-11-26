using Claims.Domain;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Domain;

public enum ClaimAction
{
    Assign,
    RequestInformation,
    ProvideInformation,
    RecordAssessment,
    Approve,
    Reject,
    Settle,
}

/// <summary>Every (status, action) pair: 6 statuses x 7 actions = 42 cases.</summary>
public sealed class ClaimTransitionTests
{
    // The specification, written independently of Claim: (from, action) -> expected status and event.
    private static readonly Dictionary<(ClaimStatus From, ClaimAction Action), (ClaimStatus To, Type Event)> Allowed = new()
    {
        [(ClaimStatus.Submitted, ClaimAction.Assign)] = (ClaimStatus.UnderReview, typeof(ClaimAssigned)),
        [(ClaimStatus.UnderReview, ClaimAction.RequestInformation)] = (ClaimStatus.AwaitingInformation, typeof(InformationRequested)),
        [(ClaimStatus.AwaitingInformation, ClaimAction.ProvideInformation)] = (ClaimStatus.UnderReview, typeof(InformationProvided)),
        [(ClaimStatus.UnderReview, ClaimAction.RecordAssessment)] = (ClaimStatus.UnderReview, typeof(ClaimAssessed)),
        [(ClaimStatus.UnderReview, ClaimAction.Approve)] = (ClaimStatus.Approved, typeof(ClaimApproved)),
        [(ClaimStatus.UnderReview, ClaimAction.Reject)] = (ClaimStatus.Rejected, typeof(ClaimRejected)),
        [(ClaimStatus.Approved, ClaimAction.Settle)] = (ClaimStatus.Settled, typeof(ClaimSettled)),
    };

    public static TheoryData<ClaimStatus, ClaimAction> AllowedCases() =>
        new(Allowed.Keys.Select(k => (k.From, k.Action)));

    public static TheoryData<ClaimStatus, ClaimAction> ForbiddenCases() =>
        new(from status in Enum.GetValues<ClaimStatus>()
            from action in Enum.GetValues<ClaimAction>()
            where !Allowed.ContainsKey((status, action))
            select (status, action));

    [Fact]
    public void Matrix_covers_every_status_and_action()
    {
        var total = Enum.GetValues<ClaimStatus>().Length * Enum.GetValues<ClaimAction>().Length;
        (AllowedCases().Count + ForbiddenCases().Count).ShouldBe(total);
        AllowedCases().Count.ShouldBe(7);
    }

    [Theory]
    [MemberData(nameof(AllowedCases))]
    public void Allowed_action_moves_claim_records_history_and_raises_event(ClaimStatus from, ClaimAction action)
    {
        var claim = ClaimIn(from);
        var historyBefore = claim.StatusHistory.Count;
        var (expectedStatus, expectedEvent) = Allowed[(from, action)];

        Invoke(claim, action);

        claim.Status.ShouldBe(expectedStatus);
        claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType(expectedEvent);

        if (expectedStatus == from)
        {
            claim.StatusHistory.Count.ShouldBe(historyBefore); // not a transition
        }
        else
        {
            claim.StatusHistory.Count.ShouldBe(historyBefore + 1);
            var row = claim.StatusHistory[^1];
            row.FromStatus.ShouldBe(from);
            row.ToStatus.ShouldBe(expectedStatus);
            row.ClaimId.ShouldBe(claim.Id);
        }
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public void Forbidden_action_throws_and_leaves_claim_unchanged(ClaimStatus from, ClaimAction action)
    {
        var claim = ClaimIn(from);
        var historyBefore = claim.StatusHistory.Count;
        var requestsBefore = claim.InformationRequests.Count;
        var reserveBefore = claim.Reserve;
        var decisionBefore = claim.Decision;

        Should.Throw<DomainException>(() => Invoke(claim, action));

        claim.Status.ShouldBe(from);
        claim.StatusHistory.Count.ShouldBe(historyBefore);
        claim.InformationRequests.Count.ShouldBe(requestsBefore);
        claim.Reserve.ShouldBe(reserveBefore);
        claim.Decision.ShouldBeSameAs(decisionBefore);
        claim.DomainEvents.ShouldBeEmpty();
    }

    private static void Invoke(Claim claim, ClaimAction action)
    {
        switch (action)
        {
            case ClaimAction.Assign:
                claim.Assign(OfficerId, Now);
                break;
            case ClaimAction.RequestInformation:
                claim.RequestInformation(OfficerId, "Please send photos of the damage.", Now);
                break;
            case ClaimAction.ProvideInformation:
                claim.ProvideInformation(OpenRequestId(claim), ClaimantId, "Photos attached.", Now);
                break;
            case ClaimAction.RecordAssessment:
                claim.RecordAssessment(OfficerId, Money(2_800m), null, Now);
                break;
            case ClaimAction.Approve:
                claim.Approve(OfficerId, Money(2_500m), "Covered loss", Now);
                break;
            case ClaimAction.Reject:
                claim.Reject(OfficerId, "Excluded peril", Now);
                break;
            case ClaimAction.Settle:
                claim.Settle(OfficerId, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }
}
