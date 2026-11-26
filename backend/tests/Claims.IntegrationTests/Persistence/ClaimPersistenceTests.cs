using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Claims.IntegrationTests.Persistence;

/// <summary>Each test creates its own claim, so seeded rows are never changed.</summary>
public sealed class ClaimPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Midnight UTC today: inside the seeded policy period, and exact under PostgreSQL's microsecond precision.
    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Claim_round_trips_with_children_money_and_decision()
    {
        var (claimId, officerId) = await SubmitNewClaimAsync();

        // Change a tracked, already-persisted aggregate: new children must be INSERTed, not UPDATEd.
        await using (var db = fixture.CreateDbContext())
        {
            var claim = await LoadAsync(db, claimId);
            claim.Assign(officerId, Now.AddMinutes(1));
            claim.RequestInformation(officerId, "Please send the repair quote.", Now.AddMinutes(2));
            claim.ProvideInformation(claim.ClaimantId, "Quote attached.", Now.AddMinutes(3));
            claim.RecordAssessment(officerId, Money(claim, 900.50m), "Quote checked.", Now.AddMinutes(4));
            claim.Approve(officerId, Money(claim, 900.50m), "Covered loss.", Now.AddMinutes(5));
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = fixture.CreateDbContext())
        {
            var claim = await LoadAsync(db, claimId);

            claim.Status.ShouldBe(ClaimStatus.Approved);
            claim.AssignedOfficerId.ShouldBe(officerId);
            claim.ClaimedAmount.ShouldBe(Money(claim, 1_234.56m));
            claim.Reserve.ShouldBe(Money(claim, 900.50m));
            claim.AssessmentNotes.ShouldBe("Quote checked.");

            claim.Decision.ShouldNotBeNull();
            claim.Decision.Outcome.ShouldBe(DecisionOutcome.Approved);
            claim.Decision.ApprovedAmount.ShouldBe(Money(claim, 900.50m));

            var request = claim.InformationRequests.ShouldHaveSingleItem();
            request.Answer.ShouldBe("Quote attached.");
            request.AnsweredAt.ShouldBe(Now.AddMinutes(3));

            claim.StatusHistory.OrderBy(h => h.ChangedAt).Select(h => h.ToStatus).ShouldBe(
            [
                ClaimStatus.Submitted, ClaimStatus.UnderReview, ClaimStatus.AwaitingInformation,
                ClaimStatus.UnderReview, ClaimStatus.Approved,
            ]);
        }
    }

    [Fact]
    public async Task Unapproved_claim_has_no_reserve_or_decision()
    {
        var (claimId, _) = await SubmitNewClaimAsync();

        await using var db = fixture.CreateDbContext();
        var claim = await LoadAsync(db, claimId);

        claim.Reserve.ShouldBeNull();
        claim.Decision.ShouldBeNull();
    }

    [Fact]
    public async Task Duplicate_claim_number_is_rejected_by_the_database()
    {
        await using var db = fixture.CreateDbContext();
        var policy = await db.Policies.FirstAsync(Ct);
        var number = await ClaimNumberSequence.NextAsync(db, Ct);

        db.Claims.Add(NewClaim(number, policy));
        await db.SaveChangesAsync(Ct);

        db.Claims.Add(NewClaim(number, policy));
        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        ex.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Concurrent_updates_to_the_same_claim_conflict_on_xmin()
    {
        var (claimId, officerId) = await SubmitNewClaimAsync();

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var claimA = await LoadAsync(first, claimId);
        var claimB = await LoadAsync(second, claimId);

        claimA.Assign(officerId, Now.AddMinutes(1));
        await first.SaveChangesAsync(Ct);

        claimB.Assign(officerId, Now.AddMinutes(2));
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    private async Task<(Guid ClaimId, Guid OfficerId)> SubmitNewClaimAsync()
    {
        await using var db = fixture.CreateDbContext();
        var policy = await db.Policies.FirstAsync(p => p.Market == Market.Singapore, Ct);
        var officerId = await db.Users.Where(u => u.Role == Role.Officer).Select(u => u.Id).FirstAsync(Ct);

        var claim = NewClaim(await ClaimNumberSequence.NextAsync(db, Ct), policy);
        db.Claims.Add(claim);
        await db.SaveChangesAsync(Ct);
        return (claim.Id, officerId);
    }

    private static Claim NewClaim(string number, Policy policy)
    {
        // Seeded policies run from a year ago to a year ahead, so yesterday is always covered.
        var incidentDate = DateOnly.FromDateTime(Now.UtcDateTime.AddDays(-1));
        return Claim.Submit(number, policy, policy.HolderId, incidentDate, "Test claim", new Money(1_234.56m, policy.Currency), Now);
    }

    private static Task<Claim> LoadAsync(ClaimsDbContext db, Guid claimId) =>
        db.Claims
            .Include(c => c.StatusHistory)
            .Include(c => c.InformationRequests)
            .Include(c => c.Decision)
            .SingleAsync(c => c.Id == claimId, Ct);

    private static Money Money(Claim claim, decimal amount) => new(amount, claim.Currency);
}
