using Claims.Application.Claims;
using Claims.Application.Common;
using Claims.Domain;
using NSubstitute;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Application;

public sealed class ClaimantClaimServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IPolicyRepository _policies = Substitute.For<IPolicyRepository>();
    private readonly IClaimRepository _claims = Substitute.For<IClaimRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClaimantClaimService _service;

    private static readonly Actor Owner = new(ClaimantId, Role.Claimant);
    private static readonly Actor OtherClaimant = new(Guid.CreateVersion7(), Role.Claimant);

    public ClaimantClaimServiceTests()
    {
        _claims.NextClaimNumberAsync(Arg.Any<CancellationToken>()).Returns(ClaimNumber);
        _claims.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Detail(call.Arg<Guid>(), ClaimantId));
        _service = new ClaimantClaimService(_policies, _claims, _outbox, _unitOfWork, new FixedClock(Now));
    }

    [Fact]
    public async Task Submit_stages_events_in_the_outbox_before_saving()
    {
        _policies.FindByNumberAsync("POL-SG-0001", Arg.Any<CancellationToken>()).Returns(Policy());

        await _service.SubmitAsync(Owner, NewRequest(" POL-SG-0001 "), Ct);

        Received.InOrder(() =>
        {
            _claims.Add(Arg.Is<Claim>(c => c.ClaimantId == ClaimantId));
            _outbox.Enqueue(Arg.Any<Claim>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Submit_on_someone_elses_policy_is_not_found_and_saves_nothing()
    {
        _policies.FindByNumberAsync("POL-SG-0001", Arg.Any<CancellationToken>()).Returns(Policy());

        await Should.ThrowAsync<NotFoundException>(() => _service.SubmitAsync(OtherClaimant, NewRequest("POL-SG-0001"), Ct));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_someone_elses_claim_is_forbidden_for_a_claimant_but_not_for_staff()
    {
        var id = Guid.CreateVersion7();

        await Should.ThrowAsync<ForbiddenException>(() => _service.GetAsync(OtherClaimant, id, Ct));
        (await _service.GetAsync(new Actor(OfficerId, Role.Officer), id, Ct)).Id.ShouldBe(id);
    }

    [Fact]
    public async Task Respond_on_someone_elses_claim_is_forbidden_and_changes_nothing()
    {
        var claim = ClaimIn(ClaimStatus.AwaitingInformation);
        _claims.GetForUpdateAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await Should.ThrowAsync<ForbiddenException>(() => _service.RespondToInformationRequestAsync(
            OtherClaimant, claim.Id, OpenRequestId(claim), new InformationResponseRequest("Sent."), Ct));

        claim.Status.ShouldBe(ClaimStatus.AwaitingInformation);
        _outbox.DidNotReceiveWithAnyArgs().Enqueue(default!);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Respond_to_a_request_that_is_not_on_the_claim_is_not_found()
    {
        var claim = ClaimIn(ClaimStatus.AwaitingInformation);
        _claims.GetForUpdateAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await Should.ThrowAsync<NotFoundException>(() => _service.RespondToInformationRequestAsync(
            Owner, claim.Id, Guid.CreateVersion7(), new InformationResponseRequest("Sent."), Ct));
    }

    [Fact]
    public async Task Respond_answers_the_request_then_stages_events_and_saves()
    {
        var claim = ClaimIn(ClaimStatus.AwaitingInformation);
        _claims.GetForUpdateAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await _service.RespondToInformationRequestAsync(Owner, claim.Id, OpenRequestId(claim), new InformationResponseRequest("Sent."), Ct);

        claim.Status.ShouldBe(ClaimStatus.UnderReview);
        Received.InOrder(() =>
        {
            _outbox.Enqueue(claim);
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    private static SubmitClaimRequest NewRequest(string policyNumber) =>
        new(policyNumber, ProductType.Motor, IncidentDate, "Rear-ended", 1_000m, Currency);

    private static ClaimDetail Detail(Guid id, Guid claimantId) => new(
        id, ClaimNumber, "POL-SG-0001", claimantId, ProductType.Motor, Market.Singapore, ClaimStatus.Submitted,
        IncidentDate, "Rear-ended", new MoneyDto(1_000m, Currency), new MoneyDto(9_500m, Currency), Now,
        null, [], []);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
