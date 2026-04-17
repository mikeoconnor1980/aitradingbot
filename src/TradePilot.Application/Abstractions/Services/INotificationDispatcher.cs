using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Unified notification dispatcher that routes events to both real-time (SignalR)
/// and out-of-band (Telegram) channels. Callers no longer need to coordinate
/// <see cref="ISignalRPublisher"/> and <see cref="ITelegramNotifier"/> separately.
/// </summary>
public interface INotificationDispatcher
{
    Task NotifyFillAsync(FillEventDto fill, CancellationToken cancellationToken = default);

    Task NotifyFillBatchAsync(IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default);

    Task NotifyOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default);

    Task NotifyConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default);

    Task NotifyUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default);

    Task NotifyStrategyEventAsync(string eventType, string strategyName, string? detail = null, CancellationToken cancellationToken = default);

    Task NotifyRiskEventAsync(string eventType, string message, CancellationToken cancellationToken = default);
}
