using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Execution boundary interface. Live mode uses Hyperliquid execution,
/// and backtest mode uses simulated execution.
/// </summary>
public interface IExecutionEngine
{
    Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default);
    Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default);
    Task<string> PlaceTriggerOrderAsync(string asset, string side, decimal size, decimal triggerPrice, string tpslType, CancellationToken cancellationToken = default);
    Task ModifyTriggerOrderAsync(string orderId, string asset, string side, decimal triggerPrice, decimal size, string tpslType, CancellationToken cancellationToken = default);
    Task SetLeverageAsync(string asset, int leverage, bool isIsolated, CancellationToken cancellationToken = default);
}
