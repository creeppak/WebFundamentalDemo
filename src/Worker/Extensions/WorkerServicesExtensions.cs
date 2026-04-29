using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Worker.Analysis;
using Worker.Analysis.Claude;
using Worker.Jobs;
using Worker.Mappers;
using Worker.MarketData;
using Worker.MarketData.AlphaVantage;
using Worker.MarketData.Finnhub;

namespace Worker.Extensions;

public static class WorkerServicesExtensions
{
    public static IServiceCollection AddWorkerJobs(
        this IServiceCollection services,
        string finnhubApiKey,
        string anthropicApiKey,
        string alphaVantageApiKey)
    {
        services.AddHttpClient<FinnhubHttpClient>((_, client) =>
        {
            client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
            client.DefaultRequestHeaders.Add("X-Finnhub-Token", finnhubApiKey);
        })
        .AddPolicyHandler((sp, _) => FinnhubPolicies.CreateRateLimitPolicy(
            sp.GetRequiredService<ILogger<FinnhubHttpClient>>()))
        .AddPolicyHandler(FinnhubPolicies.RetryPolicy)
        .AddPolicyHandler(FinnhubPolicies.CircuitBreakerPolicy);

        services.AddScoped<ICompanyDataProvider, FinnhubMarketDataProvider>();

        services.AddTransient<AlphaVantageApiKeyHandler>(_ => new AlphaVantageApiKeyHandler(alphaVantageApiKey));
        services.AddHttpClient<AlphaVantageHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.alphavantage.co/");
        })
        .AddHttpMessageHandler<AlphaVantageApiKeyHandler>()
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        services.AddScoped<IPriceDataProvider, AlphaVantagePriceProvider>();

        services.AddHttpClient<AnthropicHttpClient>((_, client) =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.DefaultRequestHeaders.Add("x-api-key", anthropicApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        })
        .AddPolicyHandler((sp, _) => AnthropicPolicies.CreateRateLimitPolicy(
            sp.GetRequiredService<ILogger<AnthropicHttpClient>>()))
        .AddPolicyHandler(AnthropicPolicies.RetryPolicy);

        services.AddScoped<IAnalysisGenerator, ClaudeAnalysisGenerator>();

        services.AddSingleton<JobMapper>();
        services.AddScoped<PriceSyncJob>();
        services.AddScoped<FundamentalsSyncJob>();
        services.AddScoped<NewsSyncJob>();
        services.AddScoped<AnalysisGenerationJob>();
        services.AddScoped<JobOrchestrator>();

        return services;
    }
}
