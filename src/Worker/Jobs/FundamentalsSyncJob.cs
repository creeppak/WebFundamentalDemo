using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Worker.MarketData;

namespace Worker.Jobs;

public class FundamentalsSyncJob(
    IMarketDataProvider marketData,
    AppDbContext db,
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
            existing.MarketCap     = snapshot.MarketCap;
            existing.PeRatio       = snapshot.PeRatio;
            existing.EpsAnnual     = snapshot.EpsAnnual;
            existing.WeekHigh52    = snapshot.WeekHigh52;
            existing.WeekLow52     = snapshot.WeekLow52;
            existing.DividendYield = snapshot.DividendYield;
            existing.Sector        = snapshot.Sector;
            existing.Industry      = snapshot.Industry;
        }
        else
        {
            db.Fundamentals.Add(new Fundamental
            {
                Ticker        = ticker,
                Date          = today,
                MarketCap     = snapshot.MarketCap,
                PeRatio       = snapshot.PeRatio,
                EpsAnnual     = snapshot.EpsAnnual,
                WeekHigh52    = snapshot.WeekHigh52,
                WeekLow52     = snapshot.WeekLow52,
                DividendYield = snapshot.DividendYield,
                Sector        = snapshot.Sector,
                Industry      = snapshot.Industry,
            });
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("FundamentalsSyncJob: upserted snapshot for {Ticker}", ticker);
    }
}