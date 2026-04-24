using System.Net;
using System.Net.Http.Json;

namespace Worker.Analysis.Claude;

public class AnthropicHttpClient(HttpClient httpClient)
{
    internal async Task<MessagesResponse?> CreateMessageAsync(MessagesRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("v1/messages", request, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Anthropic rate limit exceeded (429).", null, HttpStatusCode.TooManyRequests);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: ct);
    }
}