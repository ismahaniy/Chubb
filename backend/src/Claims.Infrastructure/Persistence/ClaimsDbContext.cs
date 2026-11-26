using Claims.Domain;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Persistence;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    /// <summary>Name of the shadow property on Claim that maps to PostgreSQL's xmin (row version).</summary>
    public const string ClaimVersionProperty = "Version";

    public DbSet<MarketDetails> Markets => Set<MarketDetails>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimStatusHistory> ClaimStatusHistory => Set<ClaimStatusHistory>();
    public DbSet<InformationRequest> InformationRequests => Set<InformationRequest>();
    public DbSet<ClaimDecision> ClaimDecisions => Set<ClaimDecision>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(ClaimNumberSequence.Name);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored by name so the database stays readable and reordering members is safe.
        configurationBuilder.Properties<Market>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ProductType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<ClaimStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<DecisionOutcome>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Role>().HaveConversion<string>().HaveMaxLength(32);
    }
}
