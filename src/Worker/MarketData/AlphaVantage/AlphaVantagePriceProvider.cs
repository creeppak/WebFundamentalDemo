using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Worker.MarketData.AlphaVantage;

public class AlphaVantagePriceProvider(
    AlphaVantageHttpClient client,
    ILogger<AlphaVantagePriceProvider> logger) : IPriceDataProvider
{
    public async Task<IReadOnlyList<PriceBar>> GetPricesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var response = await client.GetDailySeriesAsync(ticker, ct);

        if (response?.Information is not null || response?.Note is not null)
            throw new AlphaVantageRateLimitException(ticker);

        if (response?.TimeSeries is null)
        {
            logger.LogDebug("No price data for {Ticker} ({From}–{To})", ticker, from, to);
            return [];
        }

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
