namespace Shared.Portfolio;

public record HoldingDto(
    string Ticker,
    string CompanyName,
    decimal Quantity,
    decimal AverageCostBasis,
    decimal? CurrentPrice,
    decimal? MarketValue,
    decimal? UnrealizedPnL,
    decimal? UnrealizedPnLPercent);
