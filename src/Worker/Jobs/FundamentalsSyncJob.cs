using Infrastructure.Data;
using Infrastructure.Domain;
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
        var jobRun = new JobRun
        {
            JobName   = nameof(FundamentalsSyncJob),
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
                logger.LogWarning("FundamentalsSyncJob: no tickers in stocks table — skipping");
            }
            else
            {
                logger.LogInformation(
                    "FundamentalsSyncJob starting: {TickerCount} tickers", tickers.Count);

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
            logger.LogError(ex, "FundamentalsSyncJob encountered an unhandled error");
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
        var snapshot = await marketData.GetFundamentalsAsync(ticker, ct);

        if (snapshot is null)
        {
            logger.LogDebug("No fundamentals data returned for {Ticker}", ticker);
            return;
        }

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
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
