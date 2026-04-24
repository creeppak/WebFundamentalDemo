using System.Text.Json.Serialization;

namespace Worker.MarketData.Finnhub;

internal sealed record CandleResponse(
    [property: JsonPropertyName("s")] string Status,
    [property: JsonPropertyName("t")] IReadOnlyList<long>? Timestamps,
    [property: JsonPropertyName("o")] IReadOnlyList<decimal>? Open,
    [property: JsonPropertyName("h")] IReadOnlyList<decimal>? High,
    [property: JsonPropertyName("l")] IReadOnlyList<decimal>? Low,
    [property: JsonPropertyName("c")] IReadOnlyList<decimal>? Close,
    [property: JsonPropertyName("v")] IReadOnlyList<decimal>? Volume);

internal sealed record MetricResponse(
    [property: JsonPropertyName("metric")] MetricData? Metric);

internal sealed record MetricData(
    [property: JsonPropertyName("marketCapitalization")] decimal? MarketCap,
    [property: JsonPropertyName("peBasicExclExtraTTM")] decimal? PeRatio,
    [property: JsonPropertyName("epsBasicExclExtraAnnual")] decimal? EpsAnnual,
    [property: JsonPropertyName("52WeekHigh")] decimal? WeekHigh52,
    [property: JsonPropertyName("52WeekLow")] decimal? WeekLow52,
    [property: JsonPropertyName("dividendYieldIndicatedAnnual")] decimal? DividendYield);

internal sealed record NewsItemResponse(
    [property: JsonPropertyName("datetime")] long Datetime,
    [property: JsonPropertyName("headline")] string? Headline,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("source")] string? Source);
