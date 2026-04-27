using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");
        builder.HasKey(s => s.Ticker);
        builder.Property(s => s.Ticker).HasMaxLength(10).IsRequired();
        builder.Property(s => s.CompanyName).HasMaxLength(200).IsRequired();

        builder.HasData(
            new Stock { Ticker = "AAPL", CompanyName = "Apple Inc." },
            new Stock { Ticker = "MSFT", CompanyName = "Microsoft Corporation" },
            new Stock { Ticker = "GOOGL", CompanyName = "Alphabet Inc." },
            new Stock { Ticker = "AMZN", CompanyName = "Amazon.com Inc." },
            new Stock { Ticker = "META", CompanyName = "Meta Platforms Inc." },
            new Stock { Ticker = "TSLA", CompanyName = "Tesla Inc." },
            new Stock { Ticker = "NVDA", CompanyName = "NVIDIA Corporation" },
            new Stock { Ticker = "JPM",  CompanyName = "JPMorgan Chase & Co." }
        );
    }
}