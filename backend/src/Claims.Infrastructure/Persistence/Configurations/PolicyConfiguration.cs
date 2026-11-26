using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> b)
    {
        b.ToTable("policies");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();

        b.Property(p => p.PolicyNumber).HasMaxLength(32);
        b.HasIndex(p => p.PolicyNumber).IsUnique();

        b.ComplexProperty(p => p.CoverageLimit, m => m.MapMoney("coverage_limit"));
        b.ComplexProperty(p => p.Deductible, m => m.MapMoney("deductible"));
        b.Ignore(p => p.Currency);
        b.Ignore(p => p.MaxPayable);

        b.HasOne<User>().WithMany().HasForeignKey(p => p.HolderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MarketDetails>().WithMany().HasForeignKey(p => p.Market).OnDelete(DeleteBehavior.Restrict);
    }
}
