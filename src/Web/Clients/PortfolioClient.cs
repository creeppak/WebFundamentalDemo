using System.Net.Http.Json;
using Shared.Portfolio;

namespace Web.Clients;

public class PortfolioClient(HttpClient http)
{
    public async Task<PortfolioDto?> GetPortfolioAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<PortfolioDto>("api/portfolio", ct);

    public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<TransactionDto>>("api/portfolio/transactions", ct) ?? [];

    // On failure (404 ticker not found, 422 insufficient funds) throws HttpRequestException with StatusCode set.
    // Callers should catch and inspect ex.StatusCode for user-facing error messages.
    public async Task<PortfolioDto?> BuyAsync(BuyRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/portfolio/buy", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PortfolioDto>(cancellationToken: ct);
    }

    public async Task<PortfolioDto?> SellAsync(SellRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/portfolio/sell", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PortfolioDto>(cancellationToken: ct);
    }
}
