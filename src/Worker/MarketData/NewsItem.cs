namespace Worker.MarketData;

public record NewsItem(
    string Ticker,
    string Headline,
    string Url,
    string? Source,
    DateTimeOffset PublishedAt);