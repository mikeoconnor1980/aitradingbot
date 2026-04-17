using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class MarketDataStreamServiceTests
{
    private readonly Mock<IHyperliquidWebSocketClient> _wsClientMock = new();
    private readonly Mock<ISignalRPublisher> _publisherMock = new();
    private readonly Mock<IHyperliquidRestClient> _restClientMock = new();
    private readonly Mock<IHyperliquidAccountService> _accountServiceMock = new();
    private readonly Mock<ILogger<MarketDataStreamService>> _loggerMock = new();

    [TestInitialize]
    public void Setup()
    {
        _publisherMock
            .Setup(p => p.BroadcastPriceUpdateAsync(It.IsAny<PriceUpdateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock
            .Setup(p => p.BroadcastConnectionStatusAsync(It.IsAny<ConnectionStatusDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock
            .Setup(p => p.BroadcastOrdersResyncAsync(It.IsAny<IReadOnlyList<OpenOrderDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock
            .Setup(p => p.BroadcastPositionsResyncAsync(It.IsAny<IReadOnlyList<PositionDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _accountServiceMock
            .Setup(a => a.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OpenOrderDto>)new List<OpenOrderDto>());
        _accountServiceMock
            .Setup(a => a.GetPositionsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PositionDto>)new List<PositionDto>());
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
        catch (OperationCanceledException) { }
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
            catch (OperationCanceledException) { }
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
        catch (OperationCanceledException) { }
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

            // Wait for ExecuteAsync to register the trade handler
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (tradeHandler is null && DateTime.UtcNow < deadline)
                await Task.Delay(50, cts.Token);

            tradeHandler.Should().NotBeNull();

            await tradeHandler!(new TradeTickDto
            {
                Asset = "BTC-PERP",
                Price = 95123m,
                Size = 0.5m,
                Side = "B",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });

            await Task.Delay(1500, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _publisherMock.Verify(
            p => p.BroadcastPriceUpdateAsync(
                It.Is<PriceUpdateDto>(dto =>
                    dto.Asset == "BTC-PERP" &&
                    dto.LastPrice == 95123m),
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(4000, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _wsClientMock.Verify(
            w => w.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [TestMethod]
    public async Task GivenStreamService_WhenWebSocketReconnects_ThenResyncsOrdersAndPositions()
    {
        var connectionCount = 0;

        _restClientMock
            .Setup(r => r.GetMarketInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        _wsClientMock
            .Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock
            .Setup(w => w.SubscribeToTradesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock
            .Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                connectionCount++;
                if (connectionCount == 1)
                    throw new InvalidOperationException("Connection lost");
                return Task.Delay(Timeout.Infinite, ct);
            });

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(3000, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _accountServiceMock.Verify(
            a => a.GetOpenOrdersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _accountServiceMock.Verify(
            a => a.GetPositionsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private MarketDataStreamService CreateService()
    {
        return new MarketDataStreamService(
            _wsClientMock.Object,
            _publisherMock.Object,
            _restClientMock.Object,
            _accountServiceMock.Object,
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
