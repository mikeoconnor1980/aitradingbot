using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IHyperliquidWebSocketClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SubscribeToTradesAsync(string coin, CancellationToken cancellationToken = default);

    void OnTradeReceived(Func<TradeTickDto, Task> handler);

    void OnConnectionStateChanged(Func<WebSocketConnectionState, Task> handler);

    Task ReceiveLoopAsync(CancellationToken cancellationToken = default);
}