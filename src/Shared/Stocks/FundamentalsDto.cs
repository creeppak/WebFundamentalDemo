namespace Shared.Stocks;

public record FundamentalsDto(
    DateOnly Date,
    decimal? MarketCap,
    decimal? PeRatio,
    decimal? EpsAnnual,
    decimal? WeekHigh52,
    decimal? WeekLow52,
    decimal? DividendYield,
    string? Sector,
    string? Industry);