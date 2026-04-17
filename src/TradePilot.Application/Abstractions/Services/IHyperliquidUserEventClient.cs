using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// WebSocket client for Hyperliquid per-wallet user event subscriptions (fills, order updates).
/// Manages its own WebSocket connection, separate from the market data client.
/// </summary>
public interface IHyperliquidUserEventClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SubscribeToUserEventsAsync(string walletAddress, CancellationToken cancellationToken = default);

    void OnFillReceived(Func<FillEventDto, Task> handler);

    void OnFillBatchReceived(Func<IReadOnlyList<FillEventDto>, Task> handler);

    void OnOrderUpdateReceived(Func<OrderUpdateDto, Task> handler);

    void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler);

    Task ReceiveLoopAsync(CancellationToken cancellationToken = default);
}
