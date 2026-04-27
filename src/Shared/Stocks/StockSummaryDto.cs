namespace Shared.Stocks;

public record StockSummaryDto(
    string Ticker,
    string CompanyName,
    decimal? LatestClose,
    decimal? DayChangePercent);
