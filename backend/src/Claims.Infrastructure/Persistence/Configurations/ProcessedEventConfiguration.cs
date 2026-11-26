using Claims.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> b)
    {
        b.ToTable("processed_events");
        b.HasKey(e => new { e.EventId, e.Consumer });
        b.Property(e => e.Consumer).HasMaxLength(100);
    }
}
