using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Worker.Mappers;
using Worker.MarketData;

namespace Worker.Jobs;

public class NewsSyncJob(
    IMarketDataProvider marketData,
    AppDbContext db,
    JobMapper mapper,
    ILogger<NewsSyncJob> logger)
{
    private const int HeadlinesPerTicker = 5;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var tickers = await db.Stocks.Select(s => s.Ticker).ToListAsync(ct);

        if (tickers.Count == 0)
        {
            logger.LogWarning("NewsSyncJob: no tickers in stocks table — skipping");
            return;
        }

        logger.LogInformation("NewsSyncJob starting: {TickerCount} tickers", tickers.Count);

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
                logger.LogError(ex, "NewsSyncJob failed for {Ticker}", ticker);
                failed++;
            }
        }

        logger.LogInformation(
            "NewsSyncJob complete: {Synced} succeeded, {Failed} failed",
            synced, failed);
    }

    private async Task SyncTickerAsync(string ticker, CancellationToken ct)
    {
        var items = await marketData.GetNewsAsync(ticker, HeadlinesPerTicker, ct);

        if (items.Count == 0)
        {
            logger.LogDebug("No news returned for {Ticker}", ticker);
            return;
        }

        // Deduplicate by URL: Finnhub's free tier has no article ID field.
        var incomingUrls = items.Select(i => i.Url).ToHashSet();
        var existingUrls = (await db.NewsArticles
            .Where(n => n.Ticker == ticker && incomingUrls.Contains(n.Url))
            .Select(n => n.Url)
            .ToListAsync(ct))
            .ToHashSet();

        var newArticles = items
            .Where(i => !existingUrls.Contains(i.Url))
            .Select(mapper.ToNewsArticle)
            .ToList();

        if (newArticles.Count == 0)
        {
            logger.LogDebug("No new articles for {Ticker}", ticker);
            return;
        }

        db.NewsArticles.AddRange(newArticles);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "NewsSyncJob: inserted {Count} new articles for {Ticker}",
            newArticles.Count, ticker);
    }
}