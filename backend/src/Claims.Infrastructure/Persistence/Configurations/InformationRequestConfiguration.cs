using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class InformationRequestConfiguration : IEntityTypeConfiguration<InformationRequest>
{
    public void Configure(EntityTypeBuilder<InformationRequest> b)
    {
        b.ToTable("information_requests");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();

        b.Property(r => r.Question).HasMaxLength(2000);
        b.Property(r => r.Answer).HasMaxLength(4000);
        b.Ignore(r => r.IsOpen);

        b.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
