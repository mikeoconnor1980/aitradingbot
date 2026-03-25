using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TradingApp.Api.Hubs;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class MarketDataStreamServiceTests
{
    private readonly Mock<IHyperliquidWebSocketClient> _wsClientMock = new();
    private readonly Mock<IHubContext<MarketDataHub>> _hubContextMock = new();
    private readonly Mock<IHyperliquidRestClient> _restClientMock = new();
    private readonly Mock<ILogger<MarketDataStreamService>> _loggerMock = new();
    private readonly Mock<IHubClients> _hubClientsMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();

    [TestInitialize]
    public void Setup()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);
        _clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenStarted_ThenSeedsStatsFromRest()
    {
        _restClientMock
            .Setup(r => r.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketInfoDto
            {
                Asset = "BTC-PERP",
                MidPrice = 95000m,
                Volume24h = 1000000m,
            });

        SetupLongRunningWebSocket();

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(500, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _restClientMock.Verify(
            r => r.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenRestSeedFails_ThenContinuesWithZeroStats()
    {
        _restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("REST unavailable"));

        SetupLongRunningWebSocket();

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var act = async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(500, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }
        };

        await act.Should().NotThrowAsync();
    }

    [TestMethod]
    public async Task GivenStreamService_WhenWebSocketConnects_ThenSubscribesToBtcTrades()
    {
        _restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        SetupLongRunningWebSocket();

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(500, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _wsClientMock.Verify(
            w => w.SubscribeToTradesAsync("BTC", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenTradeReceived_ThenBroadcastsPriceUpdate()
    {
        Func<TradeTickDto, Task>? tradeHandler = null;

        _restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        _wsClientMock
            .Setup(w => w.OnTradeReceived(It.IsAny<Func<TradeTickDto, Task>>()))
            .Callback<Func<TradeTickDto, Task>>(handler => tradeHandler = handler);

        SetupLongRunningWebSocket();

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            await service.StartAsync(cts.Token);
            tradeHandler.Should().NotBeNull();

            await tradeHandler!(new TradeTickDto
            {
                Asset = "BTC-PERP",
                Price = 95123m,
                Size = 0.5m,
                Side = "B",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });

            await Task.Delay(700, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _clientProxyMock.Verify(
            p => p.SendCoreAsync(
                "ReceivePriceUpdate",
                It.Is<object?[]>(args =>
                    args.Length == 1 &&
                    args[0] is PriceUpdateDto &&
                    ((PriceUpdateDto)args[0]!).Asset == "BTC-PERP" &&
                    ((PriceUpdateDto)args[0]!).LastPrice == 95123m),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenConnectionFails_ThenRetriesWithBackoff()
    {
        _restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        _wsClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection failed"));

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2300));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(2200, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _wsClientMock.Verify(
            w => w.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    private MarketDataStreamService CreateService()
    {
        return new MarketDataStreamService(
            _wsClientMock.Object,
            _hubContextMock.Object,
            _restClientMock.Object,
            _loggerMock.Object);
    }

    private void SetupLongRunningWebSocket()
    {
        _wsClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock
            .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));
    }
}
