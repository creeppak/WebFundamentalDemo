using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class StockAnalysisConfiguration : IEntityTypeConfiguration<StockAnalysis>
{
    public void Configure(EntityTypeBuilder<StockAnalysis> builder)
    {
        builder.ToTable("analyses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Ticker).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Analysis).IsRequired();
        builder.Property(a => a.Summary).IsRequired();

        // Unique constraint + hot read-path index (latest analysis per ticker).
        builder.HasIndex(a => new { a.Ticker, a.Date })
            .IsDescending(false, true)
            .IsUnique();

        builder.HasOne<Stock>()
            .WithMany()
            .HasForeignKey(a => a.Ticker)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
