using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Worker.MarketData;

namespace Worker.Jobs;

public class PriceSyncJob(
    IMarketDataProvider marketData,
    AppDbContext db,
    ILogger<PriceSyncJob> logger)
{
    // 30 calendar days covers ~20 trading days after weekends and US holidays.
    private const int LookbackCalendarDays = 30;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var tickers = await db.Stocks.Select(s => s.Ticker).ToListAsync(ct);

        if (tickers.Count == 0)
        {
            logger.LogWarning("PriceSyncJob: no tickers in stocks table — skipping");
            return;
        }

        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-LookbackCalendarDays);

        logger.LogInformation(
            "PriceSyncJob starting: {TickerCount} tickers, {From}–{To}",
            tickers.Count, from, to);

        var synced = 0;
        var failed = 0;

        foreach (var ticker in tickers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await SyncTickerAsync(ticker, from, to, ct);
                synced++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PriceSyncJob failed for {Ticker}", ticker);
                failed++;
            }
        }

        logger.LogInformation(
            "PriceSyncJob complete: {Synced} succeeded, {Failed} failed",
            synced, failed);
    }

    private async Task SyncTickerAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var bars = await marketData.GetPricesAsync(ticker, from, to, ct);

        if (bars.Count == 0)
        {
            // Normal on weekends, market holidays, or a ticker with no recent activity.
            logger.LogDebug("No price data returned for {Ticker} ({From}–{To})", ticker, from, to);
            return;
        }

        var dates = bars.Select(b => b.Date).ToHashSet();
        var existing = await db.Prices
            .Where(p => p.Ticker == ticker && dates.Contains(p.Date))
            .ToDictionaryAsync(p => p.Date, ct);

        foreach (var bar in bars)
        {
            if (existing.TryGetValue(bar.Date, out var price))
            {
                price.Open   = bar.Open;
                price.High   = bar.High;
                price.Low    = bar.Low;
                price.Close  = bar.Close;
                price.Volume = bar.Volume;
            }
            else
            {
                db.Prices.Add(new Price
                {
                    Ticker = ticker,
                    Date   = bar.Date,
                    Open   = bar.Open,
                    High   = bar.High,
                    Low    = bar.Low,
                    Close  = bar.Close,
                    Volume = bar.Volume,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PriceSyncJob: upserted {Count} bars for {Ticker}",
            bars.Count, ticker);
    }
}