using Microsoft.Extensions.Logging;

namespace Worker.MarketData.Finnhub;

public class FinnhubMarketDataProvider(
    FinnhubHttpClient client,
    ILogger<FinnhubMarketDataProvider> logger) : ICompanyDataProvider
{
    public async Task<StockFundamentals?> GetFundamentalsAsync(string ticker, CancellationToken ct)
    {
        var response = await client.GetMetricsAsync(ticker, ct);

        if (response?.Metric is null)
        {
            logger.LogDebug("No fundamentals data for {Ticker}", ticker);
            return null;
        }

        var m = response.Metric;
        return new StockFundamentals(
            Ticker: ticker,
            MarketCap: m.MarketCap,       // Finnhub returns this in millions USD
            PeRatio: m.PeRatio,
            EpsAnnual: m.EpsAnnual,
            WeekHigh52: m.WeekHigh52,
            WeekLow52: m.WeekLow52,
            DividendYield: m.DividendYield,
            Sector: null,                  // requires a separate /stock/profile2 call
            Industry: null);
    }

    public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(string ticker, int count, CancellationToken ct)
    {
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-7);

        var items = await client.GetCompanyNewsAsync(ticker, from, to, ct);

        return items
            .Where(n => n.Headline is not null && n.Url is not null)
            .OrderByDescending(n => n.Datetime)
            .Take(count)
            .Select(n => new NewsItem(
                Ticker: ticker,
                Headline: n.Headline!,
                Url: n.Url!,
                Source: n.Source,
                PublishedAt: DateTimeOffset.FromUnixTimeSeconds(n.Datetime)))
            .ToList();
    }
}