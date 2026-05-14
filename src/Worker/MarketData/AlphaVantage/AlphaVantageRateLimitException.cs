namespace Worker.MarketData.AlphaVantage;

public sealed class AlphaVantageRateLimitException(string ticker)
    : Exception($"Alpha Vantage rate limit hit for {ticker}.");
