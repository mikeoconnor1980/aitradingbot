using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Infrastructure.Services;

/// <summary>
/// No-op Telegram notifier used when no bot token is configured.
/// </summary>
public sealed class NullTelegramNotifier : ITelegramNotifier
{
    public Task NotifyFillAsync(long chatId, FillEventDto fill, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyFillBatchAsync(long chatId, IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyRiskEventAsync(long chatId, string eventType, string message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyStrategyEventAsync(long chatId, string eventType, string strategyName, string? detail = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
