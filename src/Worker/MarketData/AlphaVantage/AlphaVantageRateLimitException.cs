namespace Worker.MarketData.AlphaVantage;

public sealed class AlphaVantageRateLimitException(string ticker, int retries)
    : Exception($"Alpha Vantage rate limit still active for {ticker} after {retries} retries.");
