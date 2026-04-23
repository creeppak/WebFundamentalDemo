using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransactionType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.Price)
            .HasColumnType("decimal(18,4)");

        builder.Property(t => t.Quantity)
            .HasColumnType("decimal(18,4)");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}