namespace Infrastructure.Domain;

public class StockAnalysis
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime GeneratedAt { get; set; }
}