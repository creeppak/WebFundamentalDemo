using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class FundamentalConfiguration : IEntityTypeConfiguration<Fundamental>
{
    public void Configure(EntityTypeBuilder<Fundamental> builder)
    {
        builder.ToTable("fundamentals");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Ticker).HasMaxLength(10).IsRequired();
        builder.Property(f => f.MarketCap).HasColumnType("decimal(18,4)");
        builder.Property(f => f.PeRatio).HasColumnType("decimal(18,4)");
        builder.Property(f => f.EpsAnnual).HasColumnType("decimal(18,4)");
        builder.Property(f => f.WeekHigh52).HasColumnType("decimal(18,4)");
        builder.Property(f => f.WeekLow52).HasColumnType("decimal(18,4)");
        builder.Property(f => f.DividendYield).HasColumnType("decimal(18,4)");

        // Unique constraint + hot read-path index (latest snapshot per ticker).
        builder.HasIndex(f => new { f.Ticker, f.Date })
            .IsDescending(false, true)
            .IsUnique();

        builder.HasOne<Stock>()
            .WithMany()
            .HasForeignKey(f => f.Ticker)
            .OnDelete(DeleteBehavior.Cascade);
    }
}