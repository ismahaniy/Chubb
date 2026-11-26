using Claims.Domain;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Domain;

public sealed class ClaimRuleTests
{
    // ---- Submit ----

    [Fact]
    public void Submit_creates_submitted_claim_with_history_event_and_policy_snapshot()
    {
        var policy = Policy();

        var claim = Claim.Submit(ClaimNumber, policy, ClaimantId, ProductType.Motor, IncidentDate, "  Hail damage  ", Money(3_000m), Now);

        claim.Status.ShouldBe(ClaimStatus.Submitted);
        claim.ClaimNumber.ShouldBe(ClaimNumber);
        claim.PolicyId.ShouldBe(policy.Id);
        claim.Market.ShouldBe(Market.Singapore);
        claim.ProductType.ShouldBe(ProductType.Motor);
        claim.MaxPayable.ShouldBe(Money(9_500m));
        claim.Description.ShouldBe("Hail damage");

        var row = claim.StatusHistory.ShouldHaveSingleItem();
        row.FromStatus.ShouldBeNull();
        row.ToStatus.ShouldBe(ClaimStatus.Submitted);
        row.ChangedBy.ShouldBe(ClaimantId);

        var submitted = claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ClaimSubmitted>();
        submitted.ClaimId.ShouldBe(claim.Id);
        submitted.ClaimedAmount.ShouldBe(Money(3_000m));
    }

    [Fact]
    public void Submit_by_someone_other_than_policy_holder_throws() =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), Guid.CreateVersion7(), ProductType.Motor, IncidentDate, "x", Money(100m), Now));

    [Theory]
    [InlineData(2025, 12, 31)]
    [InlineData(2027, 1, 1)]
    public void Submit_outside_policy_period_throws(int y, int m, int d) =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, new DateOnly(y, m, d), "x", Money(100m), Now));

    [Fact]
    public void Submit_in_other_currency_throws() =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "x", new Money(100m, "HKD"), Now));

    [Fact]
    public void Submit_zero_amount_throws() =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "x", Money(0m), Now));

    [Fact]
    public void Submit_blank_description_throws() =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "   ", Money(100m), Now));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_blank_claim_number_throws(string claimNumber) =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(claimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "x", Money(100m), Now));

    [Fact]
    public void Times_are_stored_in_utc()
    {
        var local = new DateTimeOffset(2026, 3, 1, 17, 0, 0, TimeSpan.FromHours(8));

        var claim = Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "x", Money(100m), local);
        claim.Assign(OfficerId, local);

        claim.SubmittedAt.Offset.ShouldBe(TimeSpan.Zero);
        claim.StatusHistory.ShouldAllBe(h => h.ChangedAt.Offset == TimeSpan.Zero);
        claim.StatusHistory[^1].ChangedAt.ShouldBe(local);
    }

    [Fact]
    public void Sla_is_due_seven_days_after_submission_and_does_not_move()
    {
        var claim = SubmittedClaim();

        claim.SlaDueAt.ShouldBe(Now.AddDays(7));
        claim.SlaDueAt.Offset.ShouldBe(TimeSpan.Zero);

        claim.Assign(OfficerId, Now.AddDays(1));
        claim.RequestInformation(OfficerId, "Photos please.", Now.AddDays(2));
        claim.ProvideInformation(OpenRequestId(claim), ClaimantId, "Sent.", Now.AddDays(5));

        claim.SlaDueAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public void Submit_for_a_product_the_policy_does_not_cover_throws() =>
        Should.Throw<DomainException>(() =>
            Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Property, IncidentDate, "x", Money(100m), Now))
            .Message.ShouldContain("Motor");

    [Fact]
    public void Submit_on_a_policy_that_has_expired_throws_even_for_an_incident_inside_its_period()
    {
        var afterExpiry = new DateTimeOffset(2027, 1, 5, 9, 0, 0, TimeSpan.Zero);

        Should.Throw<DomainException>(() =>
                Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, IncidentDate, "x", Money(100m), afterExpiry))
            .Message.ShouldContain("not active");
    }

    [Fact]
    public void Submit_with_an_incident_date_in_the_future_throws() =>
        Should.Throw<DomainException>(() =>
                Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1), "x", Money(100m), Now))
            .Message.ShouldContain("future");

    [Fact]
    public void Today_is_the_market_date_not_the_utc_date()
    {
        // 23:30 UTC on 1 March is 07:30 on 2 March in Singapore: an incident on 2 March is not in the future there.
        var lateUtc = new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero);

        var claim = Claim.Submit(ClaimNumber, Policy(), ClaimantId, ProductType.Motor, new DateOnly(2026, 3, 2), "x", Money(100m), lateUtc);

        claim.IncidentDate.ShouldBe(new DateOnly(2026, 3, 2));
    }

    [Theory]
    [InlineData(Market.Singapore, 8)]
    [InlineData(Market.HongKong, 8)]
    [InlineData(Market.Malaysia, 8)]
    [InlineData(Market.Philippines, 8)]
    [InlineData(Market.Thailand, 7)]
    [InlineData(Market.Indonesia, 7)]
    public void Every_market_has_a_utc_offset(Market market, int hours) =>
        MarketClock.UtcOffset(market).ShouldBe(TimeSpan.FromHours(hours));

    // ---- Approve: coverage limit minus deductible ----

    [Fact]
    public void Approve_exactly_max_payable_succeeds()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);

        claim.Approve(OfficerId, Money(9_500m), "Total loss", Now);

        claim.Status.ShouldBe(ClaimStatus.Approved);
        claim.Decision!.Outcome.ShouldBe(DecisionOutcome.Approved);
        claim.Decision.ApprovedAmount.ShouldBe(Money(9_500m));
        claim.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ClaimApproved>().ApprovedAmount.ShouldBe(Money(9_500m));
    }

    [Fact]
    public void Approve_above_max_payable_throws()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);

        Should.Throw<DomainException>(() => claim.Approve(OfficerId, Money(9_500.01m), "Total loss", Now))
            .Message.ShouldContain("maximum payable");

        claim.Status.ShouldBe(ClaimStatus.UnderReview);
        claim.Decision.ShouldBeNull();
    }

    [Fact]
    public void Approve_in_other_currency_throws() =>
        Should.Throw<DomainException>(() =>
            ClaimIn(ClaimStatus.UnderReview).Approve(OfficerId, new Money(100m, "HKD"), "ok", Now));

    [Fact]
    public void Approve_zero_throws() =>
        Should.Throw<DomainException>(() =>
            ClaimIn(ClaimStatus.UnderReview).Approve(OfficerId, Money(0m), "ok", Now));

    [Fact]
    public void Approve_without_reason_throws() =>
        Should.Throw<DomainException>(() =>
            ClaimIn(ClaimStatus.UnderReview).Approve(OfficerId, Money(100m), " ", Now));

    // ---- Reserve before decision ----

    [Fact]
    public void Approve_without_reserve_throws()
    {
        var claim = SubmittedClaim();
        claim.Assign(OfficerId, Now);
        claim.ClearDomainEvents();

        Should.Throw<DomainException>(() => claim.Approve(OfficerId, Money(100m), "ok", Now))
            .Message.ShouldContain("reserve");

        claim.Status.ShouldBe(ClaimStatus.UnderReview);
        claim.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Reject_without_reserve_throws()
    {
        var claim = SubmittedClaim();
        claim.Assign(OfficerId, Now);

        Should.Throw<DomainException>(() => claim.Reject(OfficerId, "Excluded", Now))
            .Message.ShouldContain("reserve");

        claim.Status.ShouldBe(ClaimStatus.UnderReview);
    }

    [Fact]
    public void Reject_without_reason_throws() =>
        Should.Throw<DomainException>(() => ClaimIn(ClaimStatus.UnderReview).Reject(OfficerId, "", Now));

    [Fact]
    public void Reject_records_decision_without_amount()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);

        claim.Reject(OfficerId, "Excluded peril", Now);

        claim.Decision!.Outcome.ShouldBe(DecisionOutcome.Rejected);
        claim.Decision.ApprovedAmount.ShouldBeNull();
        claim.Decision.Reason.ShouldBe("Excluded peril");
    }

    // ---- Assessment ----

    [Fact]
    public void RecordAssessment_can_revise_reserve_and_does_not_change_status()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);
        var historyBefore = claim.StatusHistory.Count;

        claim.RecordAssessment(OfficerId, Money(4_000m), "Revised quote", Now);

        claim.Reserve.ShouldBe(Money(4_000m));
        claim.AssessmentNotes.ShouldBe("Revised quote");
        claim.StatusHistory.Count.ShouldBe(historyBefore);
    }

    [Fact]
    public void RecordAssessment_in_other_currency_throws() =>
        Should.Throw<DomainException>(() =>
            ClaimIn(ClaimStatus.UnderReview).RecordAssessment(OfficerId, new Money(100m, "MYR"), null, Now));

    [Fact]
    public void RecordAssessment_zero_reserve_throws() =>
        Should.Throw<DomainException>(() =>
            ClaimIn(ClaimStatus.UnderReview).RecordAssessment(OfficerId, Money(0m), null, Now));

    // ---- Information loop ----

    [Fact]
    public void Information_loop_opens_and_answers_one_request()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);

        claim.RequestInformation(OfficerId, "Police report?", Now);
        var request = claim.InformationRequests.ShouldHaveSingleItem();
        request.IsOpen.ShouldBeTrue();

        claim.ProvideInformation(OpenRequestId(claim), ClaimantId, "Attached.", Now.AddHours(2));

        request.IsOpen.ShouldBeFalse();
        request.Answer.ShouldBe("Attached.");
        claim.DomainEvents.OfType<InformationProvided>().ShouldHaveSingleItem().RequestId.ShouldBe(request.Id);
    }

    [Fact]
    public void Blank_question_or_answer_throws()
    {
        Should.Throw<DomainException>(() => ClaimIn(ClaimStatus.UnderReview).RequestInformation(OfficerId, " ", Now));

        var awaiting = ClaimIn(ClaimStatus.AwaitingInformation);
        Should.Throw<DomainException>(() => awaiting.ProvideInformation(OpenRequestId(awaiting), ClaimantId, "", Now));
    }

    [Fact]
    public void Answering_an_old_request_throws_and_leaves_the_open_one_open()
    {
        var claim = ClaimIn(ClaimStatus.UnderReview);
        claim.RequestInformation(OfficerId, "Photos?", Now);
        var first = claim.InformationRequests[^1];
        claim.ProvideInformation(first.Id, ClaimantId, "Sent.", Now);
        claim.RequestInformation(OfficerId, "Police report?", Now);
        var second = claim.InformationRequests[^1];

        Should.Throw<DomainException>(() => claim.ProvideInformation(first.Id, ClaimantId, "Again.", Now));

        second.IsOpen.ShouldBeTrue();
        first.Answer.ShouldBe("Sent.");
        claim.Status.ShouldBe(ClaimStatus.AwaitingInformation);
    }

    // ---- Full path ----

    [Fact]
    public void Full_path_records_every_transition_in_order()
    {
        var claim = SubmittedClaim();
        claim.Assign(OfficerId, Now);
        claim.RequestInformation(OfficerId, "Photos?", Now);
        claim.ProvideInformation(OpenRequestId(claim), ClaimantId, "Sent.", Now);
        claim.RecordAssessment(OfficerId, Money(2_000m), null, Now);
        claim.Approve(OfficerId, Money(1_800m), "Covered", Now);
        claim.Settle(OfficerId, Now);

        claim.StatusHistory.Select(h => (h.FromStatus, h.ToStatus)).ShouldBe(
        [
            (null, ClaimStatus.Submitted),
            (ClaimStatus.Submitted, ClaimStatus.UnderReview),
            (ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation),
            (ClaimStatus.AwaitingInformation, ClaimStatus.UnderReview),
            (ClaimStatus.UnderReview, ClaimStatus.Approved),
            (ClaimStatus.Approved, ClaimStatus.Settled),
        ]);

        claim.DomainEvents.Select(e => e.GetType()).ShouldBe(
        [
            typeof(ClaimSubmitted), typeof(ClaimAssigned), typeof(InformationRequested), typeof(InformationProvided),
            typeof(ClaimAssessed), typeof(ClaimApproved), typeof(ClaimSettled),
        ]);
        claim.DomainEvents.Select(e => e.EventId).ShouldBeUnique();
        claim.DomainEvents.ShouldAllBe(e => e.ClaimId == claim.Id);
        claim.DomainEvents.OfType<ClaimSettled>().Single().SettledAmount.ShouldBe(Money(1_800m));
        claim.AssignedOfficerId.ShouldBe(OfficerId);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_list()
    {
        var claim = SubmittedClaim();
        claim.ClearDomainEvents();
        claim.DomainEvents.ShouldBeEmpty();
    }
}
