using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ClaimDecisionConfiguration : IEntityTypeConfiguration<ClaimDecision>
{
    public void Configure(EntityTypeBuilder<ClaimDecision> b)
    {
        b.ToTable("claim_decisions");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).ValueGeneratedNever();

        b.Property(d => d.Reason).HasMaxLength(2000);
        b.ComplexProperty(d => d.ApprovedAmount, m => m.MapMoney("approved"));

        b.HasOne<User>().WithMany().HasForeignKey(d => d.DecidedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
