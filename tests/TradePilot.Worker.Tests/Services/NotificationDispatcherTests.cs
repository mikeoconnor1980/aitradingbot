using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Worker.Services;

namespace TradePilot.Worker.Tests.Services;

[TestClass]
public sealed class NotificationDispatcherTests
{
    private readonly Mock<ISignalRPublisher> _signalRPublisherMock = new();
    private readonly Mock<ITelegramNotifier> _telegramNotifierMock = new();
    private readonly NotificationConfigHolder _notificationConfig = new();
    private readonly Mock<ILogger<NotificationDispatcher>> _loggerMock = new();

    [TestInitialize]
    public void Setup()
    {
        _signalRPublisherMock
            .Setup(publisher => publisher.BroadcastFillEventAsync(It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _telegramNotifierMock
            .Setup(notifier => notifier.NotifyFillAsync(It.IsAny<long>(), It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task GivenLinkedTelegram_WhenSingleFillNotified_ThenDispatchesToSignalRAndTelegram()
    {
        _notificationConfig.TelegramChatId = 123456789;
        var dispatcher = CreateDispatcher();
        var fill = new FillEventDto
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
        };

        await dispatcher.NotifyFillAsync(fill);

        _signalRPublisherMock.Verify(
            publisher => publisher.BroadcastFillEventAsync(fill, It.IsAny<CancellationToken>()),
            Times.Once);
        _telegramNotifierMock.Verify(
            notifier => notifier.NotifyFillAsync(123456789, fill, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoLinkedTelegram_WhenSingleFillNotified_ThenOnlyDispatchesToSignalR()
    {
        var dispatcher = CreateDispatcher();
        var fill = new FillEventDto
        {
            Timestamp = DateTime.UtcNow,
            Asset = "ETH",
            Side = "Sell",
            Direction = "Open Short",
            Size = 1.25m,
            Price = 2500m,
            Fee = 1.1m,
            ClosedPnl = 0m,
            OrderId = "67890"
        };

        await dispatcher.NotifyFillAsync(fill);

        _signalRPublisherMock.Verify(
            publisher => publisher.BroadcastFillEventAsync(fill, It.IsAny<CancellationToken>()),
            Times.Once);
        _telegramNotifierMock.Verify(
            notifier => notifier.NotifyFillAsync(It.IsAny<long>(), It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private NotificationDispatcher CreateDispatcher()
    {
        return new NotificationDispatcher(
            _signalRPublisherMock.Object,
            _telegramNotifierMock.Object,
            _notificationConfig,
            _loggerMock.Object);
    }
}