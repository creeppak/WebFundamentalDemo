using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Worker.MarketData.AlphaVantage;

public class AlphaVantagePriceProvider(
    AlphaVantageHttpClient client,
    ILogger<AlphaVantagePriceProvider> logger,
    TimeSpan? rateLimitDelay = null) : IPriceDataProvider
{
    private readonly TimeSpan _rateLimitDelay = rateLimitDelay ?? TimeSpan.FromSeconds(65);
    private const int MaxRateLimitRetries = 3;

    public async Task<IReadOnlyList<PriceBar>> GetPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        DailyTimeSeriesResponse? response = null;

        for (var attempt = 0; attempt <= MaxRateLimitRetries; attempt++)
        {
            response = await client.GetDailySeriesAsync(ticker, ct);

            if (response?.TimeSeries is not null)
                break;

            if (response?.Information is not null || response?.Note is not null)
            {
                if (attempt == MaxRateLimitRetries)
                    throw new AlphaVantageRateLimitException(ticker, MaxRateLimitRetries);

                logger.LogWarning(
                    "Alpha Vantage rate limit reached for {Ticker} — waiting {Delay}s before retry ({Attempt}/{Max})",
                    ticker, _rateLimitDelay.TotalSeconds, attempt + 1, MaxRateLimitRetries);

                await Task.Delay(_rateLimitDelay, ct);
            }
            else
            {
                logger.LogDebug("No price data for {Ticker} ({From}–{To})", ticker, from, to);
                return [];
            }
        }

        if (response?.TimeSeries is null)
            return [];

        var bars = new List<PriceBar>();
        foreach (var (dateStr, bar) in response.TimeSeries)
        {
            if (!DateOnly.TryParse(dateStr, out var date) || date < from || date > to)
                continue;

            bars.Add(new PriceBar(
                Ticker: ticker,
                Date: date,
                Open:   decimal.Parse(bar.Open   ?? "0", CultureInfo.InvariantCulture),
                High:   decimal.Parse(bar.High   ?? "0", CultureInfo.InvariantCulture),
                Low:    decimal.Parse(bar.Low    ?? "0", CultureInfo.InvariantCulture),
                Close:  decimal.Parse(bar.Close  ?? "0", CultureInfo.InvariantCulture),
                Volume: long.Parse(bar.Volume    ?? "0", CultureInfo.InvariantCulture)));
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));
        return bars;
    }
}
