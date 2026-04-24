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
}