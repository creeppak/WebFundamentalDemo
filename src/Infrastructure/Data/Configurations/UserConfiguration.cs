using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.UserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired();
    }
}
