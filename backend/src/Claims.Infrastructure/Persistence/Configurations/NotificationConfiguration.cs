using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).ValueGeneratedNever();
        b.Property(n => n.Message).HasMaxLength(1000);

        b.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Claim>().WithMany().HasForeignKey(n => n.ClaimId).OnDelete(DeleteBehavior.Restrict);

        // Unread notifications per recipient.
        b.HasIndex(n => new { n.RecipientId, n.ReadAt });
    }
}
