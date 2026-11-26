using System.ComponentModel.DataAnnotations;
using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Domain;
using NSubstitute;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Application;

public sealed class OfficerClaimServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly OfficerClaimService _service;

    // ClaimTestData assigns its claims to OfficerId.
    private static readonly Actor AssignedOfficer = new(OfficerId, Role.Officer);
    private static readonly Actor OtherOfficer = new(Guid.CreateVersion7(), Role.Officer);

    public OfficerClaimServiceTests()
    {
        _claims.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Detail(call.Arg<Guid>()));
        _service = new OfficerClaimService(_claims, _outbox, _unitOfWork, new FixedClock(Now));
    }

    [Fact]
    public async Task Queue_passes_the_officer_and_the_current_time_to_the_repository()
    {
        var query = new QueueQuery(QueueScope.Mine);

        await _service.GetQueueAsync(AssignedOfficer, query, Ct);

        await _claims.Received(1).GetQueueAsync(query, OfficerId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_claim_is_not_found()
    {
        _claims.GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Claim?)null);

        await Should.ThrowAsync<NotFoundException>(() => _service.AssignAsync(AssignedOfficer, Guid.CreateVersion7(), Ct));
    }

    [Fact]
    public async Task Any_officer_can_assign_a_submitted_claim_and_it_is_staged_then_saved()
    {
        var claim = Arrange(ClaimStatus.Submitted);

        await _service.AssignAsync(OtherOfficer, claim.Id, Ct);

        claim.Status.ShouldBe(ClaimStatus.UnderReview);
        claim.AssignedOfficerId.ShouldBe(OtherOfficer.UserId);
        Received.InOrder(() =>
        {
            _outbox.Enqueue(claim);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    public static TheoryData<string> AssignedOnlyCommands => ["information", "assessment", "approve", "reject", "settle"];

    [Theory]
    [MemberData(nameof(AssignedOnlyCommands))]
    public async Task An_officer_who_is_not_assigned_is_forbidden_and_nothing_changes(string command)
    {
        var claim = Arrange(command == "settle" ? ClaimStatus.Approved : ClaimStatus.UnderReview);
        var statusBefore = claim.Status;

        await Should.ThrowAsync<ForbiddenException>(() => Run(command, OtherOfficer, claim.Id));

        claim.Status.ShouldBe(statusBefore);
        _outbox.DidNotReceiveWithAnyArgs().Enqueue(default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("information", ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation)]
    [InlineData("assessment", ClaimStatus.UnderReview, ClaimStatus.UnderReview)]
    [InlineData("approve", ClaimStatus.UnderReview, ClaimStatus.Approved)]
    [InlineData("reject", ClaimStatus.UnderReview, ClaimStatus.Rejected)]
    [InlineData("settle", ClaimStatus.Approved, ClaimStatus.Settled)]
    public async Task The_assigned_officer_runs_the_command_and_it_is_staged_then_saved_once(
        string command, ClaimStatus from, ClaimStatus to)
    {
        var claim = Arrange(from);

        await Run(command, AssignedOfficer, claim.Id);

        claim.Status.ShouldBe(to);
        _outbox.Received(1).Enqueue(claim);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_records_the_amount_and_reason()
    {
        var claim = Arrange(ClaimStatus.UnderReview);

        await _service.DecideAsync(AssignedOfficer, claim.Id, new DecisionRequest(DecisionOutcome.Approved, 2_000m, Currency, "Covered"), Ct);

        claim.Decision.ShouldNotBeNull();
        claim.Decision.Outcome.ShouldBe(DecisionOutcome.Approved);
        claim.Decision.ApprovedAmount.ShouldBe(Money(2_000m));
        claim.Decision.Reason.ShouldBe("Covered");
    }

    // ---- DecisionRequest validation (runs in model binding, before the service) ----

    [Theory]
    [InlineData(DecisionOutcome.Approved, null, "SGD", "ApprovedAmount")]
    [InlineData(DecisionOutcome.Approved, 100.0, null, "Currency")]
    [InlineData(DecisionOutcome.Rejected, 100.0, null, "ApprovedAmount")]
    [InlineData(DecisionOutcome.Rejected, null, "SGD", "ApprovedAmount")]
    public void Inconsistent_decision_is_invalid(DecisionOutcome outcome, double? amount, string? currency, string member)
    {
        var request = new DecisionRequest(outcome, (decimal?)amount, currency, "Reason");

        request.Validate(new ValidationContext(request)).ShouldContain(r => r.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData(DecisionOutcome.Approved, 100.0, "SGD")]
    [InlineData(DecisionOutcome.Rejected, null, null)]
    public void Consistent_decision_is_valid(DecisionOutcome outcome, double? amount, string? currency)
    {
        var request = new DecisionRequest(outcome, (decimal?)amount, currency, "Reason");

        request.Validate(new ValidationContext(request)).ShouldBeEmpty();
    }

    private Claim Arrange(ClaimStatus status)
    {
        var claim = ClaimIn(status);
        _claims.GetForUpdateAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        return claim;
    }

    private Task<ClaimDetail> Run(string command, Actor actor, Guid claimId) => command switch
    {
        "information" => _service.RequestInformationAsync(actor, claimId, new InformationRequestRequest("Photos please."), Ct),
        "assessment" => _service.RecordAssessmentAsync(actor, claimId, new AssessmentRequest(1_500m, Currency, "Quote"), Ct),
        "approve" => _service.DecideAsync(actor, claimId, new DecisionRequest(DecisionOutcome.Approved, 2_000m, Currency, "Covered"), Ct),
        "reject" => _service.DecideAsync(actor, claimId, new DecisionRequest(DecisionOutcome.Rejected, null, null, "Excluded"), Ct),
        "settle" => _service.SettleAsync(actor, claimId, Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private static ClaimDetail Detail(Guid id) => new(
        id, ClaimNumber, "POL-SG-0001", ClaimantId, ProductType.Motor, Market.Singapore, ClaimStatus.UnderReview,
        IncidentDate, "Rear-ended", new MoneyDto(1_000m, Currency), new MoneyDto(9_500m, Currency), Now,
        null, [], []);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
