namespace Shared.Portfolio;

public record PortfolioDto(
    IReadOnlyList<HoldingDto> Holdings,
    decimal CashBalance,
    decimal? TotalMarketValue,
    decimal? TotalUnrealizedPnL);
