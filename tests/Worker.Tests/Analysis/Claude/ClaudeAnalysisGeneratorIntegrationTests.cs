using Microsoft.Extensions.Logging.Abstractions;
using Worker.Analysis;
using Worker.Analysis.Claude;
using Worker.MarketData;

namespace Worker.Tests.Analysis.Claude;

public class ClaudeAnalysisGeneratorIntegrationTests
{
    [Fact]
    public async Task GenerateAsync_WithRealApi_ReturnsAnalysis()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (apiKey is null)
            return; // no key → skip; set ANTHROPIC_API_KEY to run against the real API

        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var client = new AnthropicHttpClient(httpClient);
        var generator = new ClaudeAnalysisGenerator(client, NullLogger<ClaudeAnalysisGenerator>.Instance);

        var input = new AnalysisInput(
            Ticker: "AAPL",
            RecentPrices: [new PriceBar("AAPL", DateOnly.FromDateTime(DateTime.UtcNow), 170m, 172m, 169m, 171m, 50_000_000)],
            Fundamentals: null,
            News: [],
            PreviousAnalysisSummary: null);

        var result = await generator.GenerateAsync(input, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Analysis);
        Assert.NotEmpty(result.Summary);
        Assert.True(result.InputTokens > 0);
        Assert.True(result.OutputTokens > 0);
    }
}