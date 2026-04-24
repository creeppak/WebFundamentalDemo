using System.Net;
using System.Net.Http.Json;

namespace Worker.MarketData.Finnhub;

public class FinnhubHttpClient(HttpClient httpClient)
{
    internal async Task<CandleResponse?> GetCandlesAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var url = $"stock/candle?symbol={ticker}&resolution=D&from={ToUnixSeconds(from)}&to={ToUnixSeconds(to)}";
        return await GetAsync<CandleResponse>(url, ct);
    }

    internal async Task<MetricResponse?> GetMetricsAsync(string ticker, CancellationToken ct)
    {
        return await GetAsync<MetricResponse>($"stock/metric?symbol={ticker}&metric=all", ct);
    }

    internal async Task<IReadOnlyList<NewsItemResponse>> GetCompanyNewsAsync(string ticker, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var url = $"company-news?symbol={ticker}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        return await GetAsync<IReadOnlyList<NewsItemResponse>>(url, ct) ?? [];
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        var response = await httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new FinnhubRateLimitException();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private static long ToUnixSeconds(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
}