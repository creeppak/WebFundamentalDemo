namespace Worker.MarketData;

public interface IMarketDataProvider
{
    Task<IReadOnlyList<PriceBar>> GetPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct);
    Task<StockFundamentals?> GetFundamentalsAsync(string ticker, CancellationToken ct);
    Task<IReadOnlyList<NewsItem>> GetNewsAsync(string ticker, int count, CancellationToken ct);
}