using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Worker.Analysis;
using Worker.Mappers;

namespace Worker.Jobs;

public class AnalysisGenerationJob(
    IAnalysisGenerator generator,
    AppDbContext db,
    JobMapper mapper,
    ILogger<AnalysisGenerationJob> logger)
{
    // Abort the entire run if month-to-date token spend hits this ceiling.
    // At ~1k tokens/call × 8 tickers × 31 days ≈ 248k tokens/month — well under ceiling.
    private const long MonthlyTokenCeiling = 1_000_000;

    private const int RecentPriceCount = 20;
    private const int NewsCount = 5;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!await IsWithinMonthlyBudgetAsync(today, ct))
            return;

        var tickers = await db.Stocks.Select(s => s.Ticker).ToListAsync(ct);

        if (tickers.Count == 0)
        {
            logger.LogWarning("AnalysisGenerationJob: no tickers in stocks table — skipping");
            return;
        }

        logger.LogInformation(
            "AnalysisGenerationJob starting: {TickerCount} tickers", tickers.Count);

        var synced = 0;
        var failed = 0;

        foreach (var ticker in tickers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await GenerateForTickerAsync(ticker, today, ct);
                synced++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AnalysisGenerationJob failed for {Ticker}", ticker);
                failed++;
            }
        }

        logger.LogInformation(
            "AnalysisGenerationJob complete: {Synced} succeeded, {Failed} failed",
            synced, failed);
    }

    private async Task<bool> IsWithinMonthlyBudgetAsync(DateOnly today, CancellationToken ct)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthlyTokens = await db.Analyses
            .Where(a => a.Date >= monthStart)
            .SumAsync(a => (long)a.InputTokens + a.OutputTokens, ct);

        if (monthlyTokens >= MonthlyTokenCeiling)
        {
            logger.LogError(
                "AnalysisGenerationJob: monthly token ceiling reached ({Tokens}/{Ceiling}) — skipping all analysis generation",
                monthlyTokens, MonthlyTokenCeiling);
            return false;
        }

        return true;
    }

    private async Task GenerateForTickerAsync(string ticker, DateOnly today, CancellationToken ct)
    {
        // Read fresh data populated by the three preceding jobs.
        var prices = await db.Prices
            .Where(p => p.Ticker == ticker)
            .OrderByDescending(p => p.Date)
            .Take(RecentPriceCount)
            .OrderBy(p => p.Date)   // chronological order for prompt readability
            .ToListAsync(ct);

        var latestFundamental = await db.Fundamentals
            .Where(f => f.Ticker == ticker)
            .OrderByDescending(f => f.Date)
            .FirstOrDefaultAsync(ct);

        var news = await db.NewsArticles
            .Where(n => n.Ticker == ticker)
            .OrderByDescending(n => n.PublishedAt)
            .Take(NewsCount)
            .ToListAsync(ct);

        var previousSummary = await db.Analyses
            .Where(a => a.Ticker == ticker && a.Date < today)
            .OrderByDescending(a => a.Date)
            .Select(a => a.Summary)
            .FirstOrDefaultAsync(ct);

        var input = new AnalysisInput(
            Ticker: ticker,
            RecentPrices: prices.Select(mapper.ToPriceBar).ToList(),
            Fundamentals: latestFundamental != null
                ? mapper.ToStockFundamentals(latestFundamental)
                : null,
            News: news.Select(mapper.ToNewsItem).ToList(),
            PreviousAnalysisSummary: previousSummary);

        var result = await generator.GenerateAsync(input, ct);

        if (result is null)
        {
            // GenerateAsync already logged the error. Keep whatever was there before.
            logger.LogWarning(
                "AnalysisGenerationJob: no result for {Ticker} — previous analysis retained",
                ticker);
            return;
        }

        var existing = await db.Analyses
            .FirstOrDefaultAsync(a => a.Ticker == ticker && a.Date == today, ct);

        if (existing is not null)
        {
            mapper.UpdateAnalysis(result, existing);
            existing.GeneratedAt = DateTime.UtcNow;
        }
        else
        {
            var analysis = mapper.ToStockAnalysis(result);
            analysis.Ticker      = ticker;
            analysis.Date        = today;
            analysis.GeneratedAt = DateTime.UtcNow;
            db.Analyses.Add(analysis);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "AnalysisGenerationJob: upserted analysis for {Ticker} ({Input}+{Output} tokens)",
            ticker, result.InputTokens, result.OutputTokens);
    }
}