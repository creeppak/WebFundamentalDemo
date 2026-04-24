using Hangfire;
using Hangfire.PostgreSql;
using Worker;
using Worker.MarketData;
using Worker.MarketData.Finnhub;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

var finnhubApiKey = builder.Configuration["Finnhub:ApiKey"]
    ?? throw new InvalidOperationException("Finnhub:ApiKey is not configured.");

builder.Services.AddHttpClient<FinnhubHttpClient>((_, client) =>
{
    client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
    client.DefaultRequestHeaders.Add("X-Finnhub-Token", finnhubApiKey);
})
.AddPolicyHandler(FinnhubPolicies.RetryPolicy)
.AddPolicyHandler(FinnhubPolicies.CircuitBreakerPolicy);

builder.Services.AddScoped<IMarketDataProvider, FinnhubMarketDataProvider>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString),
        new PostgreSqlStorageOptions { SchemaName = "hangfire" }));

builder.Services.AddHangfireServer();
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();
