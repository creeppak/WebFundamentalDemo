namespace Shared.Stocks;

public record StockDetailDto(
    string Ticker,
    string CompanyName,
    decimal? LatestClose,
    decimal? DayChangePercent,
    FundamentalsDto? Fundamentals,
    AnalysisDto? LatestAnalysis);