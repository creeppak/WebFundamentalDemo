using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Worker.MarketData.AlphaVantage;

namespace Worker.Tests.MarketData.AlphaVantage;

public class AlphaVantagePriceProviderTests
{
    private const string Ticker = "AAPL";

    private static readonly DateOnly From = new(2023, 11, 1);
    private static readonly DateOnly To   = new(2023, 11, 30);

    private static AlphaVantagePriceProvider BuildProvider(HttpStatusCode status, string body)
    {
        var httpClient = new HttpClient(new FakeHandler(status, body))
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };
        return new AlphaVantagePriceProvider(
            new AlphaVantageHttpClient(httpClient),
            NullLogger<AlphaVantagePriceProvider>.Instance,
            rateLimitDelay: TimeSpan.Zero);
    }

    [Fact]
    public async Task GetPricesAsync_ValidResponse_ReturnsMappedPriceBars()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            {
              "Time Series (Daily)": {
                "2023-11-14": {
                  "1. open":   "150.0000",
                  "2. high":   "155.0000",
                  "3. low":    "149.0000",
                  "4. close":  "153.0000",
                  "5. volume": "1000000"
                }
              }
            }
            """);

        var bars = await provider.GetPricesAsync(Ticker, From, To, CancellationToken.None);

        var bar = Assert.Single(bars);
        Assert.Equal(Ticker, bar.Ticker);
        Assert.Equal(new DateOnly(2023, 11, 14), bar.Date);
        Assert.Equal(150.00m, bar.Open);
        Assert.Equal(155.00m, bar.High);
        Assert.Equal(149.00m, bar.Low);
        Assert.Equal(153.00m, bar.Close);
        Assert.Equal(1_000_000L, bar.Volume);
    }

    [Fact]
    public async Task GetPricesAsync_MultipleBars_ReturnsSortedByDateAscending()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            {
              "Time Series (Daily)": {
                "2023-11-15": { "1. open": "160.0000", "2. high": "161.0000", "3. low": "159.0000", "4. close": "160.5000", "5. volume": "2000000" },
                "2023-11-13": { "1. open": "140.0000", "2. high": "141.0000", "3. low": "139.0000", "4. close": "140.5000", "5. volume": "1500000" },
                "2023-11-14": { "1. open": "150.0000", "2. high": "151.0000", "3. low": "149.0000", "4. close": "150.5000", "5. volume": "1000000" }
              }
            }
            """);

        var bars = await provider.GetPricesAsync(Ticker, From, To, CancellationToken.None);

        Assert.Equal(3, bars.Count);
        Assert.Equal(new DateOnly(2023, 11, 13), bars[0].Date);
        Assert.Equal(new DateOnly(2023, 11, 14), bars[1].Date);
        Assert.Equal(new DateOnly(2023, 11, 15), bars[2].Date);
    }

    [Fact]
    public async Task GetPricesAsync_BarsOutsideDateRange_AreFiltered()
    {
        var provider = BuildProvider(HttpStatusCode.OK, """
            {
              "Time Series (Daily)": {
                "2023-10-31": { "1. open": "100.0000", "2. high": "101.0000", "3. low": "99.0000", "4. close": "100.5000", "5. volume": "500000" },
                "2023-11-14": { "1. open": "150.0000", "2. high": "155.0000", "3. low": "149.0000", "4. close": "153.0000", "5. volume": "1000000" },
                "2023-12-01": { "1. open": "200.0000", "2. high": "201.0000", "3. low": "199.0000", "4. close": "200.5000", "5. volume": "750000" }
              }
            }
            """);

        var bars = await provider.GetPricesAsync(Ticker, From, To, CancellationToken.None);

        var bar = Assert.Single(bars);
        Assert.Equal(new DateOnly(2023, 11, 14), bar.Date);
    }

    [Fact]
    public async Task GetPricesAsync_RateLimitExhausted_ThrowsAlphaVantageRateLimitException()
    {
        // Alpha Vantage signals rate limits via a 200 with an "Information" field, not a 429.
        var provider = BuildProvider(HttpStatusCode.OK,
            """{"Information": "Thank you for using Alpha Vantage! Our standard API call frequency is 25 requests per day."}""");

        await Assert.ThrowsAsync<AlphaVantageRateLimitException>(() =>
            provider.GetPricesAsync(Ticker, From, To, CancellationToken.None));
    }

    [Fact]
    public async Task GetPricesAsync_NullTimeSeries_ReturnsEmpty()
    {
        var provider = BuildProvider(HttpStatusCode.OK, "{}");

        var bars = await provider.GetPricesAsync(Ticker, From, To, CancellationToken.None);

        Assert.Empty(bars);
    }

    [Fact]
    public async Task GetPricesAsync_ServerErrorResponse_ThrowsHttpRequestException()
    {
        var provider = BuildProvider(HttpStatusCode.ServiceUnavailable, "");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.GetPricesAsync(Ticker, From, To, CancellationToken.None));
    }

    [Fact]
    public async Task GetPricesAsync_MalformedJson_ThrowsJsonException()
    {
        var provider = BuildProvider(HttpStatusCode.OK, "{not valid json}");

        await Assert.ThrowsAsync<JsonException>(() =>
            provider.GetPricesAsync(Ticker, From, To, CancellationToken.None));
    }
}

file sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
}
