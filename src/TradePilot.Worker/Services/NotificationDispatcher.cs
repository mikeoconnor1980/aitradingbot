using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Worker.Services;

/// <summary>
/// Unified notification dispatcher that routes events to both SignalR (real-time UI)
/// and Telegram (out-of-band) channels. Centralises the Telegram guard pattern —
/// callers no longer check <see cref="NotificationConfigHolder.TelegramChatId"/> themselves.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly ISignalRPublisher _signalR;
    private readonly ITelegramNotifier _telegram;
    private readonly NotificationConfigHolder _notificationConfig;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        ISignalRPublisher signalR,
        ITelegramNotifier telegram,
        NotificationConfigHolder notificationConfig,
        ILogger<NotificationDispatcher> logger)
    {
        _signalR = signalR;
        _telegram = telegram;
        _notificationConfig = notificationConfig;
        _logger = logger;
    }

    public async Task NotifyFillAsync(FillEventDto fill, CancellationToken cancellationToken = default)
    {
        await _signalR.BroadcastFillEventAsync(fill, cancellationToken);
    }

    public async Task NotifyFillBatchAsync(IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default)
    {
        if (TryGetChatId(out var chatId))
        {
            await _telegram.NotifyFillBatchAsync(chatId, fills, cancellationToken);
        }
    }

    public async Task NotifyOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default)
    {
        await _signalR.BroadcastOrderUpdateAsync(orderUpdate, cancellationToken);

        if (TryGetChatId(out var chatId))
        {
            var emoji = orderUpdate.Status switch
            {
                "filled" => "\u2705",
                "canceled" or "cancelled" => "\u274c",
                "triggered" => "\u26a1",
                _ => "\U0001f4cb",
            };
            var text = $"{emoji} *Order {orderUpdate.Status.ToUpperInvariant()}* \u2014 {orderUpdate.Asset}\n" +
                       $"OrderId: `{orderUpdate.OrderId}`";
            await _telegram.NotifyRiskEventAsync(chatId, $"Order {orderUpdate.Status}", text, cancellationToken);
        }
    }

    public async Task NotifyConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
    {
        await _signalR.BroadcastConnectionStatusAsync(status, cancellationToken);
    }

    public async Task NotifyUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
    {
        await _signalR.BroadcastUserConnectionStatusAsync(status, cancellationToken);
    }

    public async Task NotifyStrategyEventAsync(string eventType, string strategyName, string? detail = null, CancellationToken cancellationToken = default)
    {
        if (TryGetChatId(out var chatId))
        {
            await _telegram.NotifyStrategyEventAsync(chatId, eventType, strategyName, detail, cancellationToken);
        }
    }

    public async Task NotifyRiskEventAsync(string eventType, string message, CancellationToken cancellationToken = default)
    {
        if (TryGetChatId(out var chatId))
        {
            await _telegram.NotifyRiskEventAsync(chatId, eventType, message, cancellationToken);
        }
    }

    private bool TryGetChatId(out long chatId)
    {
        var id = _notificationConfig.TelegramChatId;
        if (id is { } value)
        {
            chatId = value;
            return true;
        }

        chatId = default;
        return false;
    }
}
