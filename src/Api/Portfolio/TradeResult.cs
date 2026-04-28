using Shared.Portfolio;

namespace Api.Portfolio;

public enum TradeError { TickerNotFound, InsufficientFunds, InsufficientHoldings }

public record TradeResult
{
    public PortfolioDto? Portfolio { get; private init; }
    public TradeError? Error { get; private init; }

    public bool IsSuccess => Error is null;

    public static TradeResult Success(PortfolioDto portfolio) => new() { Portfolio = portfolio };
    public static TradeResult Fail(TradeError error) => new() { Error = error };
}