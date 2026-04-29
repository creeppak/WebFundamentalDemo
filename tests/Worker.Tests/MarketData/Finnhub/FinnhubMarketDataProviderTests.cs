using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Worker.MarketData.Finnhub;

namespace Worker.Tests.MarketData.Finnhub;

public class FinnhubMarketDataProviderTests
{
    private const string Ticker = "AAPL";

    private static FinnhubMarketDataProvider BuildProvider(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var httpClient = new HttpClient(new FakeHandler(response))
        {
            BaseAddress = new Uri("https://finnhub.io/api/v1/")
        };
        return new FinnhubMarketDataProvider(
            new FinnhubHttpClient(httpClient),
            NullLogger<FinnhubMarketDataProvider>.Instance);
    }

    // ── GetFundamentalsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetFundamentalsAsync_ValidResponse_ReturnsMappedFundamentals()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            {
              "metric": {
                "marketCapitalization": 2500000.0,
                "peBasicExclExtraTTM": 28.5,
                "epsBasicExclExtraAnnual": 6.11,
                "52WeekHigh": 199.62,
                "52WeekLow": 124.17,
                "dividendYieldIndicatedAnnual": 0.51
              }
            }
            """);

        var fundamentals = await provider.GetFundamentalsAsync(Ticker, CancellationToken.None);

        Assert.NotNull(fundamentals);
        Assert.Equal(Ticker, fundamentals.Ticker);
        Assert.Equal(2_500_000.0m, fundamentals.MarketCap);
        Assert.Equal(28.5m, fundamentals.PeRatio);
        Assert.Equal(6.11m, fundamentals.EpsAnnual);
        Assert.Equal(199.62m, fundamentals.WeekHigh52);
        Assert.Equal(124.17m, fundamentals.WeekLow52);
        Assert.Equal(0.51m, fundamentals.DividendYield);
    }

    [Fact]
    public async Task GetFundamentalsAsync_NullMetric_ReturnsNull()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """{"metric":null}""");

        var fundamentals = await provider.GetFundamentalsAsync(Ticker, CancellationToken.None);

        Assert.Null(fundamentals);
    }

    [Fact]
    public async Task GetFundamentalsAsync_RateLimitResponse_ThrowsFinnhubRateLimitException()
    {
        var provider = BuildProvider(HttpStatusCode.TooManyRequests, "");

        await Assert.ThrowsAsync<FinnhubRateLimitException>(() =>
            provider.GetFundamentalsAsync(Ticker, CancellationToken.None));
    }

    // ── GetNewsAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_ValidResponse_ReturnsSortedNewsCappedByCount()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            [
              { "datetime": 1700000000, "headline": "Older headline", "url": "https://a.com/1", "source": "Reuters" },
              { "datetime": 1700100000, "headline": "Newer headline", "url": "https://a.com/2", "source": "Bloomberg" },
              { "datetime": 1699900000, "headline": "Oldest headline", "url": "https://a.com/3", "source": "AP" }
            ]
            """);

        var news = await provider.GetNewsAsync(Ticker, count: 2, CancellationToken.None);

        Assert.Equal(2, news.Count);
        Assert.Equal("Newer headline", news[0].Headline);  // sorted desc by date
        Assert.Equal("Older headline", news[1].Headline);
        Assert.Equal(Ticker, news[0].Ticker);
        Assert.Equal("Bloomberg", news[0].Source);
    }

    [Fact]
    public async Task GetNewsAsync_EmptyArray_ReturnsEmptyList()
    {
        var provider = BuildProvider(HttpStatusCode.OK, "[]");

        var news = await provider.GetNewsAsync(Ticker, count: 5, CancellationToken.None);

        Assert.Empty(news);
    }

    [Fact]
    public async Task GetNewsAsync_ItemsWithNullHeadlineOrUrl_AreFiltered()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            [
              { "datetime": 1700000000, "headline": null, "url": "https://a.com/1", "source": "Reuters" },
              { "datetime": 1700100000, "headline": "Valid headline", "url": null, "source": "Bloomberg" },
              { "datetime": 1699900000, "headline": "Good one", "url": "https://a.com/3", "source": "AP" }
            ]
            """);

        var news = await provider.GetNewsAsync(Ticker, count: 10, CancellationToken.None);

        var item = Assert.Single(news);
        Assert.Equal("Good one", item.Headline);
    }

    [Fact]
    public async Task GetNewsAsync_ServerErrorResponse_ThrowsHttpRequestException()
    {
        var provider = BuildProvider(HttpStatusCode.InternalServerError, "");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetNewsAsync(Ticker, count: 5, CancellationToken.None));
    }
}

file sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(response);
}
