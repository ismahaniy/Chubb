using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();

        b.Property(u => u.Email).HasMaxLength(256);
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.DisplayName).HasMaxLength(200);
        b.Property(u => u.PasswordHash).HasMaxLength(256);
    }
}
