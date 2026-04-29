using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Worker;
using Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

var finnhubApiKey = builder.Configuration["Finnhub:ApiKey"]
    ?? throw new InvalidOperationException("Finnhub:ApiKey is not configured.");

var anthropicApiKey = builder.Configuration["Anthropic:ApiKey"]
    ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");

var alphaVantageApiKey = builder.Configuration["AlphaVantage:ApiKey"]
    ?? throw new InvalidOperationException("AlphaVantage:ApiKey is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddWorkerJobs(finnhubApiKey, anthropicApiKey, alphaVantageApiKey);

builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
host.Run();