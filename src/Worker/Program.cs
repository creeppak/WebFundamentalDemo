using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Worker;
using Worker.Analysis;
using Worker.Analysis.Claude;
using Worker.Jobs;
using Worker.MarketData;
using Worker.MarketData.Finnhub;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

var finnhubApiKey = builder.Configuration["Finnhub:ApiKey"]
    ?? throw new InvalidOperationException("Finnhub:ApiKey is not configured.");

var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient<FinnhubHttpClient>((_, client) =>
{
    client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
    client.DefaultRequestHeaders.Add("X-Finnhub-Token", finnhubApiKey);
})
.AddPolicyHandler((sp, _) => FinnhubPolicies.CreateRateLimitPolicy(
    sp.GetRequiredService<ILogger<FinnhubHttpClient>>()))
.AddPolicyHandler(FinnhubPolicies.RetryPolicy)
.AddPolicyHandler(FinnhubPolicies.CircuitBreakerPolicy);

builder.Services.AddScoped<IMarketDataProvider, FinnhubMarketDataProvider>();

builder.Services.AddHttpClient<AnthropicHttpClient>((_, client) =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("x-api-key", anthropicApiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
})
.AddPolicyHandler((sp, _) => AnthropicPolicies.CreateRateLimitPolicy(
    sp.GetRequiredService<ILogger<AnthropicHttpClient>>()))
.AddPolicyHandler(AnthropicPolicies.RetryPolicy);

builder.Services.AddScoped<IAnalysisGenerator, ClaudeAnalysisGenerator>();

builder.Services.AddScoped<PriceSyncJob>();
builder.Services.AddScoped<FundamentalsSyncJob>();

builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();