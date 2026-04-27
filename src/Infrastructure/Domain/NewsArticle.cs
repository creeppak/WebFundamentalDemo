namespace Infrastructure.Domain;

public class NewsArticle
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}