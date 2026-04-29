using System.Text.Json.Serialization;

namespace Worker.MarketData.AlphaVantage;

internal record DailyTimeSeriesResponse
{
    [JsonPropertyName("Time Series (Daily)")]
    public Dictionary<string, DailyBarResponse>? TimeSeries { get; init; }

    // Present when the free-tier rate limit is hit instead of returning data.
    [JsonPropertyName("Information")]
    public string? Information { get; init; }

    [JsonPropertyName("Note")]
    public string? Note { get; init; }
}

internal record DailyBarResponse
{
    [JsonPropertyName("1. open")]
    public string? Open { get; init; }

    [JsonPropertyName("2. high")]
    public string? High { get; init; }

    [JsonPropertyName("3. low")]
    public string? Low { get; init; }

    [JsonPropertyName("4. close")]
    public string? Close { get; init; }

    [JsonPropertyName("5. volume")]
    public string? Volume { get; init; }
}
