using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class PriceConfiguration : IEntityTypeConfiguration<Price>
{
    public void Configure(EntityTypeBuilder<Price> builder)
    {
        builder.ToTable("prices");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Ticker).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Open).HasColumnType("decimal(18,4)");
        builder.Property(p => p.High).HasColumnType("decimal(18,4)");
        builder.Property(p => p.Low).HasColumnType("decimal(18,4)");
        builder.Property(p => p.Close).HasColumnType("decimal(18,4)");

        // Unique constraint doubles as the hot read-path index (latest prices per ticker).
        builder.HasIndex(p => new { p.Ticker, p.Date })
            .IsDescending(false, true)
            .IsUnique();

        builder.HasOne<Stock>()
            .WithMany()
            .HasForeignKey(p => p.Ticker)
            .OnDelete(DeleteBehavior.Cascade);
    }
}