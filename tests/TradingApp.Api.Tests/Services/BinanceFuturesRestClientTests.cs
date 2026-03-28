using System.Text;
using Microsoft.Extensions.Logging;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class BinanceFuturesRestClientTests
{
    private const long StartTime = 1700000000000L;
    private const long EndTime = 1700001800000L;

    [TestMethod]
    public async Task GivenBinanceReturnsKlines_WhenGetKlinesAsync_ThenReturnsCandleSnapshotDtos()
    {
        const string payload = """
[
  [1700000000000, "50000.1", "50100.2", "49950.3", "50050.4", "12.5", 1700000899999, "0", 123, "0", "0", "0"],
  [1700000900000, "50050.4", "50200.5", "50000.6", "50150.7", "10.25", 1700001799999, "0", 98, "0", "0", "0"]
]
""";

        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);

        var result = await client.GetKlinesAsync("BTCUSDT", "15m", StartTime, EndTime);

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(1700000000000L);
        result[0].Open.Should().Be(50000.1m);
        result[0].High.Should().Be(50100.2m);
        result[0].Low.Should().Be(49950.3m);
        result[0].Close.Should().Be(50050.4m);
        result[0].Volume.Should().Be(12.5m);
        result[0].NumTrades.Should().Be(123);
        result[1].Timestamp.Should().Be(1700000900000L);
        handler.LastRequestUri.Should().Be("/fapi/v1/klines?symbol=BTCUSDT&interval=15m&startTime=1700000000000&limit=1500&endTime=1700001800000");
    }

    [TestMethod]
    public async Task GivenBinanceReturns429_WhenGetKlinesAsync_ThenThrowsRateLimitException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"msg\":\"too many requests\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetKlinesAsync("BTCUSDT", "15m", StartTime, EndTime);

        await act.Should().ThrowAsync<RateLimitException>()
            .WithMessage("*Binance rate limit exceeded*");
    }

    [TestMethod]
    public async Task GivenBinanceReturns451_WhenGetKlinesAsync_ThenThrowsDomainException()
    {
        var client = CreateClient(new FakeHttpMessageHandler(new HttpResponseMessage((HttpStatusCode)451)
        {
            Content = new StringContent("{\"msg\":\"restricted location\"}", Encoding.UTF8, "application/json"),
        }));

        var act = () => client.GetKlinesAsync("BTCUSDT", "15m", StartTime, EndTime);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Binance IP banned*");
    }

    [TestMethod]
    public async Task GivenBinanceReturnsMarkPriceKlines_WhenGetMarkPriceKlinesAsync_ThenReturnsCandleSnapshotDtos()
    {
        const string payload = """
[
  [1700000000000, "49990.1", "50090.2", "49940.3", "50040.4", "0", 1700000899999, "0", 0, "0", "0", "0"]
]
""";

        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        });

        var client = CreateClient(handler);

        var result = await client.GetMarkPriceKlinesAsync("BTCUSDT", "15m", StartTime, EndTime);

        result.Should().HaveCount(1);
        result[0].Timestamp.Should().Be(1700000000000L);
        result[0].Open.Should().Be(49990.1m);
        result[0].Close.Should().Be(50040.4m);
        handler.LastRequestUri.Should().Be("/fapi/v1/markPriceKlines?symbol=BTCUSDT&interval=15m&startTime=1700000000000&limit=1500&endTime=1700001800000");
    }

    private static BinanceFuturesRestClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://fapi.binance.com"),
        };

        var logger = new Mock<ILogger<BinanceFuturesRestClient>>();
        return new BinanceFuturesRestClient(httpClient, logger.Object);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.PathAndQuery;

            return Task.FromResult(new HttpResponseMessage(_response.StatusCode)
            {
                Content = _response.Content,
                RequestMessage = request,
            });
        }
    }
}