using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Execution boundary interface. Live mode uses Hyperliquid execution,
/// and backtest mode uses simulated execution.
/// </summary>
public interface IExecutionEngine
{
    Task<string> PlaceOrderAsync(OrderRequest order, CancellationToken cancellationToken = default);
    Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task CancelAllOrdersAsync(string symbol, CancellationToken cancellationToken = default);
}
