using Microsoft.Extensions.Logging;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;
using TradingApp.Worker.Services;

namespace TradingApp.Worker.Tests.Services;

[TestClass]
public sealed class UserEventStreamServiceTests
{
    private readonly Mock<IHyperliquidUserEventClient> _wsClientMock = new();
    private readonly Mock<ISignalRPublisher> _publisherMock = new();
    private readonly Mock<ITelegramNotifier> _telegramNotifierMock = new();
    private readonly NotificationConfigHolder _notificationConfigHolder = new();
    private readonly Mock<IHyperliquidSigner> _signerMock = new();
    private readonly Mock<ILogger<UserEventStreamService>> _loggerMock = new();

    private const string TestWalletAddress = "0x1234567890abcdef1234567890abcdef12345678";

    [TestInitialize]
    public void Setup()
    {
        _publisherMock
            .Setup(p => p.BroadcastFillEventAsync(It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock
            .Setup(p => p.BroadcastOrderUpdateAsync(It.IsAny<OrderUpdateDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _publisherMock
            .Setup(p => p.BroadcastUserConnectionStatusAsync(It.IsAny<ConnectionStatusDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _signerMock.Setup(s => s.WalletAddress).Returns(TestWalletAddress);
    }

    private UserEventStreamService CreateService()
    {
        return new UserEventStreamService(
            _wsClientMock.Object,
            _publisherMock.Object,
            _telegramNotifierMock.Object,
            _notificationConfigHolder,
            _signerMock.Object,
            _loggerMock.Object);
    }

    private void SetupLongRunningWebSocket()
    {
        _wsClientMock.Setup(w => w.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock.Setup(w => w.SubscribeToUserEventsAsync(TestWalletAddress, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _wsClientMock.Setup(w => w.ReceiveLoopAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) => await Task.Delay(Timeout.Infinite, ct));
        _wsClientMock.Setup(w => w.DisconnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenStarted_ThenConnectsAndSubscribes()
    {
        SetupLongRunningWebSocket();
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _wsClientMock.Verify(w => w.ConnectAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _wsClientMock.Verify(w => w.SubscribeToUserEventsAsync(TestWalletAddress, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenFillReceived_ThenBroadcastsViaPublisher()
    {
        SetupLongRunningWebSocket();
        Func<FillEventDto, Task>? fillHandler = null;
        _wsClientMock
            .Setup(w => w.OnFillReceived(It.IsAny<Func<FillEventDto, Task>>()))
            .Callback<Func<FillEventDto, Task>>(handler => fillHandler = handler);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            fillHandler.Should().NotBeNull();
            await fillHandler!(new FillEventDto
            {
                Timestamp = DateTime.UtcNow,
                Asset = "BTC",
                Side = "Buy",
                Direction = "Open Long",
                Size = 0.1m,
                Price = 50000m,
                Fee = 0.5m,
                ClosedPnl = 0m,
                OrderId = "12345"
            });
        }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _publisherMock.Verify(
            p => p.BroadcastFillEventAsync(It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenStreamService_WhenOrderUpdateReceived_ThenBroadcastsViaPublisher()
    {
        SetupLongRunningWebSocket();
        Func<OrderUpdateDto, Task>? orderHandler = null;
        _wsClientMock
            .Setup(w => w.OnOrderUpdateReceived(It.IsAny<Func<OrderUpdateDto, Task>>()))
            .Callback<Func<OrderUpdateDto, Task>>(handler => orderHandler = handler);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await service.StartAsync(cts.Token);
            await Task.Delay(200, cts.Token);

            orderHandler.Should().NotBeNull();
            await orderHandler!(new OrderUpdateDto
            {
                Timestamp = DateTime.UtcNow,
                OrderId = "67890",
                Asset = "ETH",
                Status = "Filled",
                FilledSize = 1.0m,
                RemainingSize = 0m
            });
        }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _publisherMock.Verify(
            p => p.BroadcastOrderUpdateAsync(It.IsAny<OrderUpdateDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
