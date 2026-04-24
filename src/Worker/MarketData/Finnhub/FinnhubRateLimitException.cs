namespace Worker.MarketData.Finnhub;

public sealed class FinnhubRateLimitException()
    : Exception("Finnhub rate limit exceeded (HTTP 429). Back off before retrying.");