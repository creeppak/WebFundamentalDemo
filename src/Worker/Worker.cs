using Microsoft.Extensions.DependencyInjection;
using Worker.Jobs;

namespace Worker;

public class WorkerService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<WorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<JobOrchestrator>();
            await orchestrator.RunAllAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Job chain was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job chain failed with an unhandled exception");
        }
        finally
        {
            // Always exit — this is a run-once process (Cloud Run Job pattern).
            lifetime.StopApplication();
        }
    }
}
