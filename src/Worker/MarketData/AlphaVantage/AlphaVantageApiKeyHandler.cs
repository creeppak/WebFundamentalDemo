namespace Worker.MarketData.AlphaVantage;

// Alpha Vantage authenticates via a query parameter rather than a header.
internal class AlphaVantageApiKeyHandler(string apiKey) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var uri = request.RequestUri!;
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        request.RequestUri = new Uri($"{uri}{separator}apikey={apiKey}");
        return base.SendAsync(request, ct);
    }
}
