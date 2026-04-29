using System.Net.Http.Json;

namespace Worker.MarketData.AlphaVantage;

public class AlphaVantageHttpClient(HttpClient httpClient)
{
    internal async Task<DailyTimeSeriesResponse?> GetDailySeriesAsync(string ticker, CancellationToken ct)
    {
        var url = $"query?function=TIME_SERIES_DAILY&symbol={ticker}&outputsize=compact";
        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DailyTimeSeriesResponse>(cancellationToken: ct);
    }
}
