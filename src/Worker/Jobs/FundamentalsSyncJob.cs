using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Worker.Mappers;
using Worker.MarketData;

namespace Worker.Jobs;

public class FundamentalsSyncJob(
    IMarketDataProvider marketData,
    AppDbContext db,
    JobMapper mapper,
    ILogger<FundamentalsSyncJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var tickers = await db.Stocks.Select(s => s.Ticker).ToListAsync(ct);

        if (tickers.Count == 0)
        {
            logger.LogWarning("FundamentalsSyncJob: no tickers in stocks table — skipping");
            return;
        }

        logger.LogInformation(
            "FundamentalsSyncJob starting: {TickerCount} tickers", tickers.Count);

        var synced = 0;
        var failed = 0;

        foreach (var ticker in tickers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await SyncTickerAsync(ticker, ct);
                synced++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FundamentalsSyncJob failed for {Ticker}", ticker);
                failed++;
            }
        }

        logger.LogInformation(
            "FundamentalsSyncJob complete: {Synced} succeeded, {Failed} failed",
            synced, failed);
    }

    private async Task SyncTickerAsync(string ticker, CancellationToken ct)
    {
        var snapshot = await marketData.GetFundamentalsAsync(ticker, ct);

        if (snapshot is null)
        {
            logger.LogDebug("No fundamentals data returned for {Ticker}", ticker);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await db.Fundamentals
            .FirstOrDefaultAsync(f => f.Ticker == ticker && f.Date == today, ct);

        if (existing is not null)
        {
            mapper.UpdateFundamental(snapshot, existing);
        }
        else
        {
            var fundamental = mapper.ToFundamental(snapshot);
            fundamental.Date = today;
            db.Fundamentals.Add(fundamental);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("FundamentalsSyncJob: upserted snapshot for {Ticker}", ticker);
    }
}