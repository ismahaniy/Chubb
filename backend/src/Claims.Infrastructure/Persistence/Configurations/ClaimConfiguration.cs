using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> b)
    {
        b.ToTable("claims");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();

        // Shadow property mapped by Npgsql to the xmin system column: the optimistic concurrency token.
        b.Property<uint>(ClaimsDbContext.ClaimVersionProperty).IsRowVersion();

        b.Property(c => c.ClaimNumber).HasMaxLength(20);
        b.HasIndex(c => c.ClaimNumber).IsUnique();

        b.Property(c => c.Description).HasMaxLength(2000);
        b.Property(c => c.AssessmentNotes).HasMaxLength(2000);

        b.ComplexProperty(c => c.ClaimedAmount, m => m.MapMoney("claimed"));
        b.ComplexProperty(c => c.MaxPayable, m => m.MapMoney("max_payable"));
        b.ComplexProperty(c => c.Reserve, m => m.MapMoney("reserve"));

        b.Ignore(c => c.Currency);
        b.Ignore(c => c.DomainEvents);

        b.HasOne<Policy>().WithMany().HasForeignKey(c => c.PolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.ClaimantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.AssignedOfficerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MarketDetails>().WithMany().HasForeignKey(c => c.Market).OnDelete(DeleteBehavior.Restrict);

        // Children belong to the aggregate: loaded and saved with the claim through its private backing lists.
        b.HasMany(c => c.StatusHistory).WithOne().HasForeignKey(h => h.ClaimId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(c => c.InformationRequests).WithOne().HasForeignKey(r => r.ClaimId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Decision).WithOne().HasForeignKey<ClaimDecision>(d => d.ClaimId).OnDelete(DeleteBehavior.Cascade);

        // Officer work queues and manager dashboards.
        b.HasIndex(c => new { c.Status, c.AssignedOfficerId });
        b.HasIndex(c => new { c.Market, c.Status });
        b.HasIndex(c => new { c.AssignedOfficerId, c.SlaDueAt });
    }
}
