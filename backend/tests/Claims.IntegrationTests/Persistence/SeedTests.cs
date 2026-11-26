using Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace Claims.IntegrationTests.Persistence;

public sealed class SeedTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_creates_markets_users_policies_and_claims()
    {
        await using var db = fixture.CreateDbContext();

        (await db.Markets.CountAsync(Ct)).ShouldBe(6);
        (await db.Policies.CountAsync(Ct)).ShouldBe(10);
        (await db.Claims.CountAsync(Ct)).ShouldBe(15);

        var roles = await db.Users.GroupBy(u => u.Role).ToDictionaryAsync(g => g.Key, g => g.Count(), Ct);
        roles.ShouldBe(new Dictionary<Role, int> { [Role.Claimant] = 2, [Role.Officer] = 4, [Role.Manager] = 1 }, ignoreOrder: true);
    }

    [Fact]
    public async Task Seed_covers_every_claim_status_and_market()
    {
        await using var db = fixture.CreateDbContext();

        var statuses = await db.Claims.Select(c => c.Status).Distinct().ToListAsync(Ct);
        statuses.ShouldBe(Enum.GetValues<ClaimStatus>(), ignoreOrder: true);

        var markets = await db.Claims.Select(c => c.Market).Distinct().ToListAsync(Ct);
        markets.ShouldBe(Enum.GetValues<Market>(), ignoreOrder: true);
    }

    [Fact]
    public async Task Seeded_claims_have_history_ending_in_their_current_status()
    {
        await using var db = fixture.CreateDbContext();

        var claims = await db.Claims.AsNoTracking().Include(c => c.StatusHistory).ToListAsync(Ct);

        claims.ShouldAllBe(c => c.ClaimNumber.StartsWith("CLM-"));
        foreach (var claim in claims)
        {
            var history = claim.StatusHistory.OrderBy(h => h.ChangedAt).ToList();
            history[0].FromStatus.ShouldBeNull();
            history[0].ToStatus.ShouldBe(ClaimStatus.Submitted);
            history[^1].ToStatus.ShouldBe(claim.Status);
        }
    }

    [Fact]
    public async Task Seeded_policies_are_in_their_market_currency()
    {
        await using var db = fixture.CreateDbContext();

        var mismatches = await db.Policies
            .Join(db.Markets, p => p.Market, m => m.Code, (p, m) => new { Policy = p.CoverageLimit.Currency, Market = m.Currency })
            .Where(x => x.Policy != x.Market)
            .CountAsync(Ct);

        mismatches.ShouldBe(0);
    }

    [Fact]
    public async Task Running_migrations_again_does_not_reseed()
    {
        await fixture.MigrateAsync(Ct);

        await using var db = fixture.CreateDbContext();
        (await db.Users.CountAsync(Ct)).ShouldBe(7);
        (await db.Claims.CountAsync(Ct)).ShouldBe(15);
    }
}
