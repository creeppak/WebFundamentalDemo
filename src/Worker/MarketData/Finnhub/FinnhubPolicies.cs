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
}
