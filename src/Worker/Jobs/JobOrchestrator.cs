using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Jobs;

public class JobOrchestrator(
    PriceSyncJob priceSync,
    FundamentalsSyncJob fundamentalsSync,
    NewsSyncJob newsSync,
    AnalysisGenerationJob analysisGeneration,
    IOptions<WorkerOptions> options,
    ILogger<JobOrchestrator> logger)
{
    public async Task RunAllAsync(CancellationToken ct)
    {
        var jobs = options.Value.Jobs;

        logger.LogInformation("Job chain starting: {Jobs}", string.Join(", ", jobs));

        if (jobs.Contains("Prices", StringComparer.OrdinalIgnoreCase))
            await priceSync.ExecuteAsync(ct);

        if (jobs.Contains("Fundamentals", StringComparer.OrdinalIgnoreCase))
            await fundamentalsSync.ExecuteAsync(ct);

        if (jobs.Contains("News", StringComparer.OrdinalIgnoreCase))
            await newsSync.ExecuteAsync(ct);

        if (jobs.Contains("Analysis", StringComparer.OrdinalIgnoreCase))
            await analysisGeneration.ExecuteAsync(ct);

        logger.LogInformation("Job chain complete");
    }
}
