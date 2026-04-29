namespace Worker.MarketData;

public interface IPriceDataProvider
{
    Task<IReadOnlyList<PriceBar>> GetPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct);
}