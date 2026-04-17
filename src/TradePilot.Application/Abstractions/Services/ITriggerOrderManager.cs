using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Manages exchange-native TP/SL trigger orders that protect open positions.
/// These orders live on the exchange and fire independently of the worker,
/// providing resilience against worker outages and mid-candle execution.
/// </summary>
public interface ITriggerOrderManager
{
    /// <summary>
    /// Places initial TP and SL trigger orders on the exchange for the current position.
    /// Skips placement if exit config is disabled, position is flat, or orders already exist.
    /// </summary>
    Task PlaceProtectionOrdersAsync(
        PositionState positionState,
        ExitConfig exitConfig,
        MarketContext context,
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates existing trigger orders when conditions change (e.g., trailing stop moves,
    /// position size changes from partial fills).
    /// </summary>
    Task UpdateProtectionOrdersAsync(
        PositionState positionState,
        ExitConfig exitConfig,
        MarketContext context,
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all exchange-native protection orders. Called before app-side exit
    /// to prevent double-execution, or on position close.
    /// </summary>
    Task CancelProtectionOrdersAsync(
        ProtectionOrderState protectionState,
        CancellationToken cancellationToken = default);
}
