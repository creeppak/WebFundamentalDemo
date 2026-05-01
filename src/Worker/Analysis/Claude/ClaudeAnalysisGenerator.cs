using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Worker.MarketData;

namespace Worker.Analysis.Claude;

public class ClaudeAnalysisGenerator(
    AnthropicHttpClient client,
    ILogger<ClaudeAnalysisGenerator> logger) : IAnalysisGenerator
{
    private const string Model = "claude-sonnet-4-5";
    private const int MaxTokens = 1024;

    private const string SystemPrompt =
        "You are a financial analyst writing concise daily stock briefs for institutional readers. " +
        "Respond only with a JSON object: {\"analysis\": \"<3 paragraphs>\", \"summary\": \"<one sentence>\"}. " +
        "Structure: what happened today / valuation view / what to watch next. " +
        "Be direct. No filler. No disclaimers.";

    public async Task<AnalysisResult?> GenerateAsync(AnalysisInput input, CancellationToken ct)
    {
        try
        {
            var userMessage = BuildUserMessage(input);
            var request = new MessagesRequest(
                Model: Model,
                MaxTokens: MaxTokens,
                System: SystemPrompt,
                Messages: [new Message("user", userMessage)]);

            var response = await client.CreateMessageAsync(request, ct);

            var text = response?.Content.FirstOrDefault(b => b.Type == "text")?.Text;
            if (text is null || response is null)
            {
                logger.LogWarning("Claude returned an empty response for {Ticker}", input.Ticker);
                return null;
            }

            logger.LogInformation(
                "Claude analysis for {Ticker}: {InputTokens} input tokens, {OutputTokens} output tokens",
                input.Ticker, response.Usage.InputTokens, response.Usage.OutputTokens);

            return ParseResponse(text, response.Usage.InputTokens, response.Usage.OutputTokens);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate analysis for {Ticker}", input.Ticker);
            return null;
        }
    }

    private static string BuildUserMessage(AnalysisInput input)
    {
        var latest = input.RecentPrices.LastOrDefault();
        var ma20 = input.RecentPrices.Count > 0
            ? input.RecentPrices.TakeLast(20).Average(p => p.Close)
            : (decimal?)null;

        var sb = new StringBuilder();
        sb.AppendLine($"Ticker: {input.Ticker}");
        sb.AppendLine($"Date: {DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        sb.AppendLine();

        sb.AppendLine("Price action:");
        if (latest is not null)
        {
            sb.AppendLine($"  Close: ${latest.Close:F2}  Open: ${latest.Open:F2}  High: ${latest.High:F2}  Low: ${latest.Low:F2}");
            sb.AppendLine($"  Volume: {latest.Volume:N0}");
        }
        if (ma20 is not null)
            sb.AppendLine($"  20-day MA: ${ma20:F2}  (close vs MA: {(latest!.Close - ma20) / ma20 * 100:+0.0;-0.0}%)");

        sb.AppendLine();
        sb.AppendLine("Fundamentals:");
        if (input.Fundamentals is { } f)
        {
            if (f.MarketCap is not null) sb.AppendLine($"  Market cap: ${f.MarketCap:N0}M");
            if (f.PeRatio is not null)   sb.AppendLine($"  P/E (TTM): {f.PeRatio:F1}");
            if (f.EpsAnnual is not null) sb.AppendLine($"  EPS (annual): ${f.EpsAnnual:F2}");
            if (f.WeekHigh52 is not null && f.WeekLow52 is not null)
                sb.AppendLine($"  52-week range: ${f.WeekLow52:F2} – ${f.WeekHigh52:F2}");
            if (f.DividendYield is not null) sb.AppendLine($"  Dividend yield: {f.DividendYield:F2}%");
        }
        else
        {
            sb.AppendLine("  Not available.");
        }

        sb.AppendLine();
        sb.AppendLine("Recent news:");
        sb.AppendLine("<news>");
        foreach (var item in input.News)
            sb.AppendLine($"  [{item.PublishedAt:yyyy-MM-dd}] {item.Source}: {item.Headline}");
        sb.AppendLine("</news>");

        if (input.PreviousAnalysisSummary is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Previous summary: {input.PreviousAnalysisSummary}");
        }

        return sb.ToString();
    }

    private static AnalysisResult ParseResponse(string text, int inputTokens, int outputTokens)
    {
        var json = ExtractJson(text);
        var parsed = JsonSerializer.Deserialize<StructuredOutput>(json)
            ?? throw new InvalidOperationException("Claude returned null when deserializing structured output.");

        if (parsed.Analysis is null || parsed.Summary is null)
            throw new InvalidOperationException(
                $"Claude response missing required fields. analysis={parsed.Analysis is null}, summary={parsed.Summary is null}");

        return new AnalysisResult(parsed.Analysis, parsed.Summary, inputTokens, outputTokens);
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf("```json", StringComparison.Ordinal);
        if (start == -1)
            return text;

        start += "```json".Length;
        var end = text.IndexOf("```", start, StringComparison.Ordinal);
        if (end == -1)
            throw new InvalidOperationException("Claude response has opening ```json fence but no closing ```.");

        return text[start..end].Trim();
    }

    private sealed record StructuredOutput(
        [property: JsonPropertyName("analysis")] string? Analysis,
        [property: JsonPropertyName("summary")] string? Summary);
}