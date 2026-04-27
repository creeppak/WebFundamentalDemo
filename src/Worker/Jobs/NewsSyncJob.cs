using Infrastructure.Data;
using Infrastructure.Domain;
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
        var jobRun = new JobRun
        {
            JobName   = nameof(NewsSyncJob),
            StartedAt = DateTime.UtcNow,
            Status    = JobRunStatus.Running,
        };
        db.JobRuns.Add(jobRun);
        await db.SaveChangesAsync(ct);

        var synced = 0;
        var failed = 0;

        try
        {
            var tickers = await db.Stocks.Select(s => s.Ticker).ToListAsync(ct);

            if (tickers.Count == 0)
            {
                logger.LogWarning("NewsSyncJob: no tickers in stocks table — skipping");
            }
            else
            {
                logger.LogInformation("NewsSyncJob starting: {TickerCount} tickers", tickers.Count);

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

            jobRun.Status = JobRunStatus.Succeeded;
        }
        catch (OperationCanceledException)
        {
            jobRun.Status       = JobRunStatus.Failed;
            jobRun.ErrorMessage = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            jobRun.Status       = JobRunStatus.Failed;
            jobRun.ErrorMessage = ex.Message;
            logger.LogError(ex, "NewsSyncJob encountered an unhandled error");
        }
        finally
        {
            jobRun.CompletedAt      = DateTime.UtcNow;
            jobRun.TickersSucceeded = synced;
            jobRun.TickersFailed    = failed;
            await db.SaveChangesAsync(CancellationToken.None);
        }
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