using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal static class MoneyMapping
{
    /// <summary>Maps a Money complex property to {prefix}_amount numeric(18,2) and {prefix}_currency varchar(3).</summary>
    public static void MapMoney(this ComplexPropertyBuilder<Money> money, string prefix)
    {
        money.Property(m => m.Amount).HasColumnName($"{prefix}_amount").HasPrecision(18, 2);
        money.Property(m => m.Currency).HasColumnName($"{prefix}_currency").HasMaxLength(3);
    }
}
