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
        _telegramNotifierMock
            .Setup(notifier => notifier.NotifyFillBatchAsync(It.IsAny<long>(), It.IsAny<IReadOnlyList<FillEventDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task GivenLinkedTelegram_WhenSingleFillNotified_ThenDispatchesToSignalROnly()
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
            Times.Never);
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

    [TestMethod]
    public async Task GivenLinkedTelegram_WhenFillBatchNotified_ThenDispatchesAllFillsToSignalRAndBatchToTelegram()
    {
        _notificationConfig.TelegramChatId = 123456789;
        var dispatcher = CreateDispatcher();
        var fills = new List<FillEventDto>
        {
            new()
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
            },
            new()
            {
                Timestamp = DateTime.UtcNow,
                Asset = "BTC",
                Side = "Buy",
                Direction = "Open Long",
                Size = 0.2m,
                Price = 50010m,
                Fee = 0.6m,
                ClosedPnl = 0m,
                OrderId = "12346"
            }
        };

        await dispatcher.NotifyFillBatchAsync(fills);

        _signalRPublisherMock.Verify(
            publisher => publisher.BroadcastFillEventAsync(It.IsAny<FillEventDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(fills.Count));
        _telegramNotifierMock.Verify(
            notifier => notifier.NotifyFillBatchAsync(123456789, It.Is<IReadOnlyList<FillEventDto>>(value => value.Count == fills.Count), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenFillDeliveredThroughSingleAndBatchPaths_WhenNotified_ThenSignalRPublishesOnceAndTelegramPublishesBatchOnce()
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
        await dispatcher.NotifyFillBatchAsync([fill]);

        _signalRPublisherMock.Verify(
            publisher => publisher.BroadcastFillEventAsync(fill, It.IsAny<CancellationToken>()),
            Times.Once);
        _telegramNotifierMock.Verify(
            notifier => notifier.NotifyFillBatchAsync(123456789, It.Is<IReadOnlyList<FillEventDto>>(value => value.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);
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