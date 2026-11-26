using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ClaimStatusHistoryConfiguration : IEntityTypeConfiguration<ClaimStatusHistory>
{
    public void Configure(EntityTypeBuilder<ClaimStatusHistory> b)
    {
        b.ToTable("claim_status_history");
        b.HasKey(h => h.Id);
        b.Property(h => h.Id).ValueGeneratedNever();

        b.HasOne<User>().WithMany().HasForeignKey(h => h.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
