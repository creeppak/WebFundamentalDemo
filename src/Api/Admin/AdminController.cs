#if DEBUG
using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Worker.Jobs;

namespace Api.Admin;

[ApiController]
[Route("admin/jobs")]
[Authorize]
public class AdminController(
    IServiceScopeFactory scopeFactory,
    AppDbContext db,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>Run the full nightly job chain (price sync → fundamentals → news → analysis) and return all resulting job run records.</summary>
    /// <response code="200">Array of job run records, one per job, in execution order.</response>
    [HttpPost("run-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobRunResponse>>> RunAll(CancellationToken ct)
    {
        var before = DateTime.UtcNow;
        logger.LogInformation("Admin trigger: running full job chain");

        await using var scope = scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<JobOrchestrator>();
        await orchestrator.RunAllAsync(ct);

        var runs = await db.JobRuns
            .Where(r => r.StartedAt >= before)
            .OrderBy(r => r.StartedAt)
            .ToListAsync(CancellationToken.None);

        return Ok(runs.Select(ToResponse).ToList());
    }

    /// <summary>Run a single job by name and return its job run record.</summary>
    /// <param name="jobName">One of: pricesync, fundamentalssync, newssync, analysisgeneration.</param>
    /// <response code="200">Job run record for the completed job.</response>
    /// <response code="404">Unknown job name, or job ran but produced no record.</response>
    [HttpPost("{jobName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobRunResponse>> RunJob(string jobName, CancellationToken ct)
    {
        var before = DateTime.UtcNow;
        logger.LogInformation("Admin trigger: running job {JobName}", jobName);

        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        switch (jobName.ToLowerInvariant())
        {
            case "pricesync":
                await sp.GetRequiredService<PriceSyncJob>().ExecuteAsync(ct);
                break;
            case "fundamentalssync":
                await sp.GetRequiredService<FundamentalsSyncJob>().ExecuteAsync(ct);
                break;
            case "newssync":
                await sp.GetRequiredService<NewsSyncJob>().ExecuteAsync(ct);
                break;
            case "analysisgeneration":
                await sp.GetRequiredService<AnalysisGenerationJob>().ExecuteAsync(ct);
                break;
            default:
                return NotFound(
                    $"Unknown job '{jobName}'. Valid names: pricesync, fundamentalssync, newssync, analysisgeneration.");
        }

        var run = (await db.JobRuns
            .Where(r => r.StartedAt >= before)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(CancellationToken.None))
            .Select(ToResponse)
            .FirstOrDefault();

        return run is not null ? Ok(run) : NotFound("Job ran but no job_run record found.");
    }

    private static JobRunResponse ToResponse(JobRun r) => new(
        r.Id,
        r.JobName,
        r.StartedAt,
        r.CompletedAt,
        r.Status.ToString(),
        r.TickersSucceeded,
        r.TickersFailed,
        r.ErrorMessage,
        r.TotalInputTokens,
        r.TotalOutputTokens);
}
#endif