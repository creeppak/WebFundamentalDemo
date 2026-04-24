using System.Net.Http.Json;

namespace Worker.Analysis.Claude;

public class AnthropicHttpClient(HttpClient httpClient)
{
    internal async Task<MessagesResponse?> CreateMessageAsync(MessagesRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("v1/messages", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: ct);
    }
}