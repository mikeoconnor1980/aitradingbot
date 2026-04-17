using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Sends Telegram notifications to a user's linked chat.
/// Implementations must swallow errors — notification failure must never disrupt trading.
/// </summary>
public interface ITelegramNotifier
{
    Task NotifyFillAsync(long chatId, FillEventDto fill, CancellationToken cancellationToken = default);

    Task NotifyFillBatchAsync(long chatId, IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default);

    Task NotifyRiskEventAsync(long chatId, string eventType, string message, CancellationToken cancellationToken = default);

    Task NotifyStrategyEventAsync(long chatId, string eventType, string strategyName, string? detail = null, CancellationToken cancellationToken = default);
}
