using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class MarketDetailsConfiguration : IEntityTypeConfiguration<MarketDetails>
{
    public void Configure(EntityTypeBuilder<MarketDetails> b)
    {
        b.ToTable("markets");
        b.HasKey(m => m.Code);
        b.Property(m => m.Code).ValueGeneratedNever();
        b.Property(m => m.Name).HasMaxLength(100);
        b.Property(m => m.Currency).HasMaxLength(3);
    }
}
