using Claims.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();

        b.Property(m => m.EventType).HasMaxLength(100);
        b.Property(m => m.CorrelationId).HasMaxLength(100);
        b.Property(m => m.Payload).HasColumnType("jsonb");

        // The publisher only scans unsent rows in occurred_at order, so index just those.
        b.HasIndex(m => m.OccurredAt).HasFilter("sent_at IS NULL");
    }
}
