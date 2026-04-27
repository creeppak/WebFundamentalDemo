using Microsoft.Extensions.Logging;

namespace Worker.Jobs;

public class JobOrchestrator(
    PriceSyncJob priceSync,
    FundamentalsSyncJob fundamentalsSync,
    NewsSyncJob newsSync,
    AnalysisGenerationJob analysisGeneration,
    ILogger<JobOrchestrator> logger)
{
    public async Task RunAllAsync(CancellationToken ct)
    {
        logger.LogInformation("Job chain starting");

        await priceSync.ExecuteAsync(ct);
        await fundamentalsSync.ExecuteAsync(ct);
        await newsSync.ExecuteAsync(ct);
        await analysisGeneration.ExecuteAsync(ct);

        logger.LogInformation("Job chain complete");
    }
}
