using System.Net;
using System.Net.Http.Json;
using Shared.Stocks;

namespace Web.Clients;

public class StocksClient(HttpClient http)
{
    public async Task<IReadOnlyList<StockSummaryDto>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<StockSummaryDto>>("api/stocks", ct) ?? [];

    public async Task<StockDetailDto?> GetByTickerAsync(string ticker, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/stocks/{ticker}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StockDetailDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<PricePointDto>> GetHistoryAsync(
        string ticker, int days = 14, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<PricePointDto>>(
            $"api/stocks/{ticker}/history?days={days}", ct) ?? [];

    public async Task<IReadOnlyList<NewsArticleDto>> GetNewsAsync(
        string ticker, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<NewsArticleDto>>(
            $"api/stocks/{ticker}/news", ct) ?? [];
}
