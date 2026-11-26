using Claims.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

/// <summary>
/// Demo data, run by EF Core's UseAsyncSeeding after migrations. Idempotent: does nothing once users exist.
/// Everything is saved in one SaveChanges, so a failed seed leaves no partial data behind.
/// Claims are built only through the aggregate's methods, so every seeded claim obeys the state machine.
/// Dates are relative to the moment of seeding so policies stay in force whenever the demo is run.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Passw0rd!";

    private static readonly (Market Code, string Name, string Currency)[] MarketData =
    [
        (Market.Singapore, "Singapore", "SGD"),
        (Market.HongKong, "Hong Kong", "HKD"),
        (Market.Malaysia, "Malaysia", "MYR"),
        (Market.Thailand, "Thailand", "THB"),
        (Market.Indonesia, "Indonesia", "IDR"),
        (Market.Philippines, "Philippines", "PHP"),
    ];

    private sealed record PolicySeed(string Number, int Holder, Market Market, ProductType Product, decimal Limit, decimal Deductible);

    // Holder 0 = claimant1, 1 = claimant2. Amounts are in the market's currency.
    private static readonly PolicySeed[] PolicyData =
    [
        new("POL-SG-0001", 0, Market.Singapore, ProductType.Motor, 50_000m, 500m),
        new("POL-SG-0002", 0, Market.Singapore, ProductType.Property, 300_000m, 1_000m),
        new("POL-MY-0001", 0, Market.Malaysia, ProductType.Motor, 80_000m, 1_000m),
        new("POL-MY-0002", 0, Market.Malaysia, ProductType.Property, 500_000m, 2_500m),
        new("POL-ID-0001", 0, Market.Indonesia, ProductType.Motor, 300_000_000m, 2_500_000m),
        new("POL-HK-0001", 1, Market.HongKong, ProductType.Motor, 250_000m, 3_000m),
        new("POL-HK-0002", 1, Market.HongKong, ProductType.Property, 2_000_000m, 10_000m),
        new("POL-TH-0001", 1, Market.Thailand, ProductType.Motor, 1_000_000m, 10_000m),
        new("POL-TH-0002", 1, Market.Thailand, ProductType.Property, 5_000_000m, 25_000m),
        new("POL-PH-0001", 1, Market.Philippines, ProductType.Property, 3_000_000m, 20_000m),
    ];

    /// <param name="Policy">Index into PolicyData.</param>
    /// <param name="DaysAgo">When the claim was submitted; later steps follow a day apart.</param>
    /// <param name="Officer">Index of the officer who picks it up (ignored while Submitted).</param>
    /// <param name="AnsweredRequest">Goes through one answered information request before the assessment.</param>
    private sealed record ClaimSeed(
        int Policy,
        int DaysAgo,
        string Description,
        decimal Claimed,
        ClaimStatus Status,
        int Officer,
        bool AnsweredRequest = false);

    // Oldest first, so claim numbers follow submission order.
    // 3 Settled, 2 Rejected, 2 Approved, 2 AwaitingInformation, 3 UnderReview, 3 Submitted.
    private static readonly ClaimSeed[] ClaimData =
    [
        new(0, 60, "Rear-ended at traffic lights on Orchard Road; bumper and boot lid damaged.", 4_200m, ClaimStatus.Settled, 0),
        new(5, 55, "Side mirror and door panel scraped in a multi-storey car park.", 18_500m, ClaimStatus.Settled, 1, AnsweredRequest: true),
        new(3, 50, "Burst pipe flooded the kitchen; cabinets and flooring need replacing.", 42_000m, ClaimStatus.Settled, 2),
        new(7, 45, "Hit a pothole on the expressway; two tyres and a wheel rim destroyed.", 38_000m, ClaimStatus.Rejected, 3),
        new(9, 42, "Water damage to the living room ceiling after heavy monsoon rain.", 120_000m, ClaimStatus.Rejected, 0, AnsweredRequest: true),
        new(1, 38, "Living room window shattered during a storm; glass and frame replacement.", 3_500m, ClaimStatus.Approved, 1),
        new(6, 35, "Kitchen fire damaged the range hood, cabinets and wall tiles.", 210_000m, ClaimStatus.Approved, 2),
        new(2, 30, "Windscreen cracked by debris from a lorry on the highway.", 1_800m, ClaimStatus.AwaitingInformation, 3),
        new(7, 27, "Rear bumper damaged by a reversing van in a shopping centre car park.", 45_000m, ClaimStatus.AwaitingInformation, 0),
        new(1, 24, "Electrical fault damaged the air-conditioning units and wiring.", 9_600m, ClaimStatus.UnderReview, 1, AnsweredRequest: true),
        new(5, 20, "Front-end collision at low speed; headlamp and grille replaced.", 22_000m, ClaimStatus.UnderReview, 2),
        new(4, 16, "Motorcycle scraped the car door while parked on the street.", 12_500_000m, ClaimStatus.UnderReview, 3),
        new(6, 12, "Ceiling leak from the flat above damaged a bedroom wardrobe.", 35_000m, ClaimStatus.Submitted, 0),
        new(0, 10, "Car keyed along the passenger side overnight.", 2_400m, ClaimStatus.Submitted, 0),
        new(9, 10, "Typhoon winds tore off part of the roof and damaged the garage door.", 450_000m, ClaimStatus.Submitted, 0),
    ];

    public static async Task SeedAsync(DbContext context, bool storeManagementPerformed, CancellationToken cancellationToken)
    {
        var db = (ClaimsDbContext)context;
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var hasher = new PasswordHasher<User>();
        User NewUser(string email, string name, Role role) => new(email, name, role, hasher.HashPassword(null!, DemoPassword));

        var markets = MarketData.Select(m => new MarketDetails(m.Code, m.Name, m.Currency)).ToList();
        var currencyOf = markets.ToDictionary(m => m.Code, m => m.Currency);

        User[] claimants =
        [
            NewUser("claimant1@claimdesk.test", "Aisha Rahman", Role.Claimant),
            NewUser("claimant2@claimdesk.test", "Daniel Wong", Role.Claimant),
        ];
        User[] officers =
        [
            NewUser("officer1@claimdesk.test", "Priya Menon", Role.Officer),
            NewUser("officer2@claimdesk.test", "Kenji Sato", Role.Officer),
            NewUser("officer3@claimdesk.test", "Nadia Halim", Role.Officer),
            NewUser("officer4@claimdesk.test", "Marco Reyes", Role.Officer),
        ];
        var manager = NewUser("manager@claimdesk.test", "Grace Lim", Role.Manager);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var policies = PolicyData
            .Select(p => new Policy(
                p.Number,
                claimants[p.Holder].Id,
                p.Market,
                p.Product,
                new Money(p.Limit, currencyOf[p.Market]),
                new Money(p.Deductible, currencyOf[p.Market]),
                today.AddMonths(-12),
                today.AddMonths(12)))
            .ToList();

        var claims = new List<Claim>(ClaimData.Length);
        foreach (var seed in ClaimData)
        {
            var claimNumber = await ClaimNumberSequence.NextAsync(db, cancellationToken);
            var claim = BuildClaim(claimNumber, policies[seed.Policy], officers[seed.Officer].Id, seed, now.AddDays(-seed.DaysAgo));

            // Seed data is history, not news: nothing goes to the outbox or Kafka.
            claim.ClearDomainEvents();
            claims.Add(claim);
        }

        db.Markets.AddRange(markets);
        db.Users.AddRange([.. claimants, .. officers, manager]);
        db.Policies.AddRange(policies);
        db.Claims.AddRange(claims);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Claim BuildClaim(string claimNumber, Policy policy, Guid officerId, ClaimSeed seed, DateTimeOffset submittedAt)
    {
        var at = submittedAt;
        DateTimeOffset Next() => at = at.AddDays(1);
        Money Amount(decimal amount) => new(amount, policy.Currency);

        var claimantId = policy.HolderId;
        var incidentDate = DateOnly.FromDateTime(submittedAt.UtcDateTime.AddDays(-2));
        var claim = Claim.Submit(claimNumber, policy, claimantId, policy.ProductType, incidentDate, seed.Description, Amount(seed.Claimed), at);
        if (seed.Status == ClaimStatus.Submitted)
        {
            return claim;
        }

        claim.Assign(officerId, Next());

        if (seed.AnsweredRequest)
        {
            claim.RequestInformation(officerId, "Please upload photos of the damage and the repair quotation.", Next());
            claim.ProvideInformation(claimantId, "Photos and the quotation are attached.", Next());
        }

        if (seed.Status == ClaimStatus.AwaitingInformation)
        {
            claim.RequestInformation(officerId, "Please provide the police or incident report reference.", Next());
            return claim;
        }

        if (seed.Status == ClaimStatus.UnderReview)
        {
            return claim;
        }

        // Reserve 90% of the claimed amount; approve the reserve.
        var reserve = Amount(decimal.Round(seed.Claimed * 0.9m, 2));
        claim.RecordAssessment(officerId, reserve, "Assessor report and repair quotation reviewed.", Next());

        if (seed.Status == ClaimStatus.Rejected)
        {
            claim.Reject(officerId, "Damage results from wear and tear, which the policy excludes.", Next());
            return claim;
        }

        claim.Approve(officerId, reserve, "Covered loss; amount agreed with the assessor.", Next());

        if (seed.Status == ClaimStatus.Settled)
        {
            claim.Settle(officerId, Next());
        }

        return claim;
    }
}
