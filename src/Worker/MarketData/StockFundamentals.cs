namespace Worker.MarketData;

public record StockFundamentals(
    string Ticker,
    decimal? MarketCap,
    decimal? PeRatio,
    decimal? EpsAnnual,
    decimal? WeekHigh52,
    decimal? WeekLow52,
    decimal? DividendYield,
    string? Sector,
    string? Industry);