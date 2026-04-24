using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceMarketMetadataProviderTests
{
    private Mock<IBinanceExchangeInfoCache> _exchangeInfoCacheMock = null!;
    private Mock<ILogger<BinanceMarketMetadataProvider>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _exchangeInfoCacheMock = new Mock<IBinanceExchangeInfoCache>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<BinanceMarketMetadataProvider>>();
    }

    [TestMethod]
    public async Task GivenValidOpenInterestResponse_WhenGetMarketInfoAsync_ThenPopulatesOpenInterest()
    {
        var handler = new RoutingHttpMessageHandler();
        handler.AddResponse(
            "/fapi/v1/premiumIndex?symbol=BTCUSDT",
            CreateJsonResponse(
                """
                {
                  "markPrice": "65000.5",
                  "indexPrice": "64999.8",
                  "lastFundingRate": "1.2E-4"
                }
                """));
        handler.AddResponse(
            "/fapi/v1/ticker/24hr?symbol=BTCUSDT",
            CreateJsonResponse(
                """
                {
                  "quoteVolume": "12345678.9",
                  "priceChangePercent": "-2.5"
                }
                """));
        handler.AddResponse(
            "/fapi/v1/openInterest?symbol=BTCUSDT",
            CreateJsonResponse(
                """
                {
                  "openInterest": "1.2345E+5",
                  "symbol": "BTCUSDT",
                  "time": 1710000000000
                }
                """));

        _exchangeInfoCacheMock
            .Setup(cache => cache.GetSymbolAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceExchangeSymbolMetadata("BTC", "BTCUSDT", 3, 2, 125));

        var sut = CreateSut(handler);

        var result = await sut.GetMarketInfoAsync(TradingPair.Create("BTC", "USD", AssetType.Perp));

        result.Should().NotBeNull();
        result!.Asset.Should().Be("BTC");
        result.MarkPrice.Should().Be(65000.5m);
        result.IndexPrice.Should().Be(64999.8m);
        result.FundingRate.Should().Be(0.00012m);
        result.Volume24h.Should().Be(12345678.9m);
        result.OpenInterest.Should().Be(123450m);
        result.PriceChange24hPercent.Should().Be(-2.5m);
    }

    [TestMethod]
    public async Task GivenOpenInterestFailure_WhenGetMarketInfoAsync_ThenDefaultsOpenInterestToZeroAndLogsWarning()
    {
        var handler = new RoutingHttpMessageHandler();
        handler.AddResponse(
            "/fapi/v1/premiumIndex?symbol=ETHUSDT",
            CreateJsonResponse(
                """
                {
                  "markPrice": "3200",
                  "indexPrice": "3195",
                  "lastFundingRate": "0.0002"
                }
                """));
        handler.AddResponse(
            "/fapi/v1/ticker/24hr?symbol=ETHUSDT",
            CreateJsonResponse(
                """
                {
                  "quoteVolume": "999999",
                  "priceChangePercent": "3.25"
                }
                """));
        handler.AddResponse(
            "/fapi/v1/openInterest?symbol=ETHUSDT",
            CreateJsonResponse("{}", HttpStatusCode.InternalServerError));

        _exchangeInfoCacheMock
            .Setup(cache => cache.GetSymbolAsync("ETH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceExchangeSymbolMetadata("ETH", "ETHUSDT", 3, 2, 125));

        var sut = CreateSut(handler);

        var result = await sut.GetMarketInfoAsync(TradingPair.Create("ETH", "USD", AssetType.Perp));

        result.Should().NotBeNull();
        result!.OpenInterest.Should().Be(0m);
        result.MarkPrice.Should().Be(3200m);

        var warningLogs = _loggerMock.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Where(invocation => invocation.Arguments[0] is LogLevel logLevel && logLevel == LogLevel.Warning)
            .ToList();

        warningLogs.Should().ContainSingle();
        warningLogs[0].Arguments[2].ToString().Should().Contain("Failed to fetch Binance open interest");
    }

    private BinanceMarketMetadataProvider CreateSut(HttpMessageHandler handler)
    {
        return new BinanceMarketMetadataProvider(
            new TestHttpClientFactory(handler),
            _exchangeInfoCacheMock.Object,
            _loggerMock.Object);
    }

    private static HttpResponseMessage CreateJsonResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://fapi.binance.com"),
            };
        }
    }

    private sealed class RoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = new(StringComparer.Ordinal);

        public void AddResponse(string pathAndQuery, HttpResponseMessage response)
        {
            _responses[pathAndQuery] = () => response;
        }

        public void AddResponse(string pathAndQuery, Func<HttpResponseMessage> responseFactory)
        {
            _responses[pathAndQuery] = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri?.PathAndQuery ?? string.Empty;
            if (!_responses.TryGetValue(key, out var responseFactory))
            {
                throw new InvalidOperationException($"No response configured for '{key}'.");
            }

            var response = responseFactory();
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}