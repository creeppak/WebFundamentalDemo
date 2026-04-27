namespace Shared.Stocks;

public record NewsArticleDto(
    string Headline,
    string Url,
    string? Source,
    DateTimeOffset PublishedAt);