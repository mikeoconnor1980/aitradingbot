using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceExchangeInfoCacheTests
{
    [TestMethod]
    public async Task GivenWarmCache_WhenGetSupportedSymbolsAsyncBeforeExpiry_ThenReturnsCachedSymbolsWithoutRefetching()
    {
        var handler = new SequenceHttpMessageHandler();
        handler.Enqueue(_ => Task.FromResult(CreateExchangeInfoResponse(("BTC", "BTCUSDT"))));

        using var sut = CreateSut(handler);

        var firstResult = await sut.GetSupportedSymbolsAsync();
        var secondResult = await sut.GetSupportedSymbolsAsync();

        firstResult.Should().ContainKey("BTC");
        secondResult.Should().ContainKey("BTC");
        handler.RequestCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenExpiredCache_WhenGetSupportedSymbolsAsync_ThenRefreshesSymbols()
    {
        var handler = new SequenceHttpMessageHandler();
        handler.Enqueue(_ => Task.FromResult(CreateExchangeInfoResponse(("BTC", "BTCUSDT"))));
        handler.Enqueue(_ => Task.FromResult(CreateExchangeInfoResponse(("ETH", "ETHUSDT"))));

        using var sut = CreateSut(handler);

        var initialResult = await sut.GetSupportedSymbolsAsync();
        SetLastRefreshTimestamp(sut, Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 3600L));

        var refreshedResult = await sut.GetSupportedSymbolsAsync();

        initialResult.Should().ContainKey("BTC");
        refreshedResult.Should().ContainKey("ETH");
        refreshedResult.Should().NotContainKey("BTC");
        handler.RequestCount.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenWaitingRequestCancellation_WhenGetSupportedSymbolsAsync_ThenSemaphoreRemainsUsable()
    {
        var enteredRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new SequenceHttpMessageHandler();
        handler.Enqueue(async cancellationToken =>
        {
            enteredRefresh.TrySetResult(true);
            await releaseRefresh.Task.WaitAsync(cancellationToken);
            return CreateExchangeInfoResponse(("BTC", "BTCUSDT"));
        });
        handler.Enqueue(_ => Task.FromResult(CreateExchangeInfoResponse(("ETH", "ETHUSDT"))));

        using var sut = CreateSut(handler);

        var firstRequest = sut.GetSupportedSymbolsAsync();
        await enteredRefresh.Task;

        using var cts = new CancellationTokenSource();
        var cancelledRequest = sut.GetSupportedSymbolsAsync(cts.Token);
        cts.Cancel();
        Func<Task> act = async () => await cancelledRequest;

        await act.Should().ThrowAsync<OperationCanceledException>();

        releaseRefresh.TrySetResult(true);
        var firstResult = await firstRequest;

        firstResult.Should().ContainKey("BTC");

        SetLastRefreshTimestamp(sut, Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 3600L));

        var refreshedResult = await sut.GetSupportedSymbolsAsync();

        refreshedResult.Should().ContainKey("ETH");
        handler.RequestCount.Should().Be(2);
    }

    private static BinanceExchangeInfoCache CreateSut(HttpMessageHandler handler)
        => new(new TestHttpClientFactory(handler));

    private static HttpResponseMessage CreateExchangeInfoResponse(params (string Asset, string Symbol)[] symbols)
    {
        var payload = JsonSerializer.Serialize(new
        {
            symbols = symbols.Select(symbol => new
            {
                symbol = symbol.Symbol,
                baseAsset = symbol.Asset,
                quoteAsset = "USDT",
                status = "TRADING",
                filters = new object[]
                {
                    new
                    {
                        filterType = "LOT_SIZE",
                        stepSize = "0.001",
                    },
                    new
                    {
                        filterType = "PRICE_FILTER",
                        tickSize = "0.10",
                    },
                },
            }),
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }

    private static void SetLastRefreshTimestamp(BinanceExchangeInfoCache cache, long timestamp)
    {
        typeof(BinanceExchangeInfoCache)
            .GetField("_lastRefreshTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(cache, timestamp);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://fapi.binance.com"),
            };
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        private int _requestCount;

        public int RequestCount => _requestCount;

        public void Enqueue(Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responses.Enqueue(responseFactory);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<HttpResponseMessage>> responseFactory;
            lock (_responses)
            {
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("No HTTP responses remain for the test handler.");
                }

                responseFactory = _responses.Dequeue();
                _requestCount++;
            }

            var response = await responseFactory(cancellationToken);
            response.RequestMessage = request;
            return response;
        }
    }
}