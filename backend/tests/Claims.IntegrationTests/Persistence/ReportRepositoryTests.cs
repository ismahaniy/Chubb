using Claims.Application.Claims;
using Claims.Application.Reports;
using Claims.Domain;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.IntegrationTests.Persistence;

/// <summary>
/// The report queries against a database of their own. Assertions compare a report before and after adding claims
/// (or count only rows this class creates), so they do not depend on the seed data. Each test adds USD claims in its
/// own market and product, so tests never see each other's rows.
/// </summary>
public sealed class ReportRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // Midnight UTC today: inside the seeded policy period, and exact under PostgreSQL's microsecond precision.
    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- exposure ----

    [Fact]
    public async Task Exposure_never_sums_across_currencies()
    {
        var before = await ExposureAsync();
        var sgdBefore = before.Single(l => IsSingaporeMotor(l) && l.Exposure.Currency == "SGD");

        var officerId = await SeededOfficerIdAsync();
        await AddClaimsAsync(Market.Singapore, ProductType.Motor, (policy, number) =>
        [
            Build(number(), policy, 1_000m, Now.AddDays(-1), ClaimStatus.Submitted, officerId),              // no reserve: claimed
            Build(number(), policy, 900m, Now.AddDays(-1), ClaimStatus.UnderReview, officerId, reserve: 700m), // reserve
            Build(number(), policy, 500m, Now.AddDays(-1), ClaimStatus.Approved, officerId, reserve: 400m),    // approved, unpaid
        ]);

        var after = await ExposureAsync();

        after.Single(l => IsSingaporeMotor(l) && l.Exposure.Currency == "SGD").ShouldBe(sgdBefore);
        var usd = after.Single(l => IsSingaporeMotor(l) && l.Exposure.Currency == "USD");
        usd.OpenClaims.ShouldBe(3);
        usd.Exposure.ShouldBe(new MoneyDto(2_100m, "USD"));
        after.Select(l => (l.Market, l.ProductType, l.Exposure.Currency)).ShouldBeUnique();
    }

    [Fact]
    public async Task Closed_claims_are_not_exposure()
    {
        var before = await ExposureAsync();

        var officerId = await SeededOfficerIdAsync();
        await AddClaimsAsync(Market.Malaysia, ProductType.Property, (policy, number) =>
        [
            Build(number(), policy, 800m, Now.AddDays(-1), ClaimStatus.Rejected, officerId, reserve: 800m),
            Build(number(), policy, 600m, Now.AddDays(-1), ClaimStatus.Settled, officerId, reserve: 600m),
        ]);

        (await ExposureAsync()).ShouldBe(before);
    }

    // ---- helpers ----

    private static bool IsSingaporeMotor(ExposureLine line) => line is { Market: Market.Singapore, ProductType: ProductType.Motor };

    private Task<IReadOnlyList<ExposureLine>> ExposureAsync() => WithRepositoryAsync(r => r.GetExposureAsync(Ct));

    /// <summary>The repository as the API resolves it, through AddInfrastructure.</summary>
    private async Task<T> WithRepositoryAsync<T>(Func<IReportRepository, Task<T>> query)
    {
        await using var scope = fixture.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<IReportRepository>());
    }

    private async Task<Guid> SeededOfficerIdAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.Users.Where(u => u.Email == "officer1@claimdesk.test").Select(u => u.Id).SingleAsync(Ct);
    }

    /// <summary>
    /// Saves a new USD policy for claimant1 in <paramref name="market"/> (seeded policies there are in the market
    /// currency) and the claims <paramref name="build"/> makes on it.
    /// </summary>
    private async Task AddClaimsAsync(Market market, ProductType product, Func<Policy, Func<string>, Claim[]> build)
    {
        await using var db = fixture.CreateDbContext();
        var holderId = await db.Users.Where(u => u.Email == "claimant1@claimdesk.test").Select(u => u.Id).SingleAsync(Ct);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var policy = new Policy(
            $"POL-USD-{market}-{product}", holderId, market, product,
            new Money(1_000_000m, "USD"), new Money(100m, "USD"), today.AddMonths(-12), today.AddMonths(12));

        var numbers = new Queue<string>();
        for (var i = 0; i < 10; i++)
        {
            numbers.Enqueue(await ClaimNumberSequence.NextAsync(db, Ct));
        }

        db.Policies.Add(policy);
        db.Claims.AddRange(build(policy, numbers.Dequeue));
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>
    /// A claim taken to <paramref name="status"/> through the aggregate, one minute per step after submission
    /// (so a Settled claim settles a few minutes after <paramref name="submittedAt"/>).
    /// </summary>
    private static Claim Build(
        string number, Policy policy, decimal claimed, DateTimeOffset submittedAt, ClaimStatus status, Guid officerId,
        decimal? reserve = null)
    {
        var at = submittedAt;
        DateTimeOffset Next() => at = at.AddMinutes(1);
        var incidentDate = DateOnly.FromDateTime(submittedAt.UtcDateTime.AddDays(-2));

        var claim = Claim.Submit(number, policy, policy.HolderId, policy.ProductType, incidentDate, "Report test claim",
            new Money(claimed, policy.Currency), at);
        if (status != ClaimStatus.Submitted)
        {
            claim.Assign(officerId, Next());
        }

        if (reserve is { } amount)
        {
            claim.RecordAssessment(officerId, new Money(amount, policy.Currency), null, Next());
        }

        switch (status)
        {
            case ClaimStatus.Approved or ClaimStatus.Settled:
                claim.Approve(officerId, claim.Reserve!, "Covered.", Next());
                break;
            case ClaimStatus.Rejected:
                claim.Reject(officerId, "Excluded.", Next());
                break;
        }

        if (status == ClaimStatus.Settled)
        {
            claim.Settle(officerId, Next());
        }

        claim.Status.ShouldBe(status);
        claim.ClearDomainEvents(); // persistence only; the outbox is covered by the API tests
        return claim;
    }
}
