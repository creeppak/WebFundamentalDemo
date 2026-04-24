using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Worker.Analysis.Claude;

internal static class AnthropicPolicies
{
    // No circuit breaker — each call costs tokens, and the monthly ceiling is the primary guard.
    internal static readonly IAsyncPolicy<HttpResponseMessage> RetryPolicy =
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    // One retry after back-off; if still rate-limited, the ceiling check is the next guard.
    internal static IAsyncPolicy<HttpResponseMessage> CreateRateLimitPolicy(ILogger logger) =>
        Policy<HttpResponseMessage>
            .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 1,
                sleepDurationProvider: (_, outcome, _) =>
                    outcome.Result?.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60),
                onRetryAsync: (outcome, delay, _, _) =>
                {
                    logger.LogWarning(
                        "Anthropic rate limit hit (429). Backing off for {Seconds}s before retry",
                        delay.TotalSeconds);
                    return Task.CompletedTask;
                });
}