using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Worker.MarketData.Finnhub;

internal static class FinnhubPolicies
{
    // Static readonly so circuit breaker state is shared across all requests, not recreated per call.
    internal static readonly IAsyncPolicy<HttpResponseMessage> RetryPolicy =
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    internal static readonly IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy =
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1));

    // Not static readonly — stateless, and needs a logger. One retry after back-off is enough;
    // if Finnhub is still rate-limiting after 60s, something unusual is happening.
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
                        "Finnhub rate limit hit (429). Backing off for {Seconds}s before retry",
                        delay.TotalSeconds);
                    return Task.CompletedTask;
                });
}
