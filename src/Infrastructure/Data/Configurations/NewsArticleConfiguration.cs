using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.ToTable("news_articles");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Ticker).HasMaxLength(10).IsRequired();
        builder.Property(n => n.Headline).IsRequired();
        builder.Property(n => n.Url).IsRequired();

        // Deduplication key: same article URL must not be stored twice for the same ticker.
        builder.HasIndex(n => new { n.Ticker, n.Url }).IsUnique();

        // Hot read-path: latest headlines per ticker.
        builder.HasIndex(n => new { n.Ticker, n.PublishedAt })
            .IsDescending(false, true);

        builder.HasOne<Stock>()
            .WithMany()
            .HasForeignKey(n => n.Ticker)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
