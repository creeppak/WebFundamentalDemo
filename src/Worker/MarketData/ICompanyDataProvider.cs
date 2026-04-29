namespace Worker.MarketData;

public interface ICompanyDataProvider
{
    Task<StockFundamentals?> GetFundamentalsAsync(string ticker, CancellationToken ct);
    Task<IReadOnlyList<NewsItem>> GetNewsAsync(string ticker, int count, CancellationToken ct);
}