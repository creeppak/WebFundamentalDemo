namespace Infrastructure.Domain;

public class Fundamental
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal? MarketCap { get; set; }   // millions USD, as returned by Finnhub
    public decimal? PeRatio { get; set; }
    public decimal? EpsAnnual { get; set; }
    public decimal? WeekHigh52 { get; set; }
    public decimal? WeekLow52 { get; set; }
    public decimal? DividendYield { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
}