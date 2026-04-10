using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Abstraction for publishing real-time messages to SignalR clients.
/// Implementations may use in-process IHubContext or Azure SignalR Management SDK.
/// </summary>
public interface ISignalRPublisher
{
    Task BroadcastPriceUpdateAsync(PriceUpdateDto update, CancellationToken cancellationToken = default);

    Task BroadcastConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default);

    Task BroadcastFillEventAsync(FillEventDto fill, CancellationToken cancellationToken = default);

    Task BroadcastOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default);

    Task BroadcastUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default);

    Task BroadcastOrdersResyncAsync(IReadOnlyList<OpenOrderDto> orders, CancellationToken cancellationToken = default);

    Task BroadcastPositionsResyncAsync(IReadOnlyList<PositionDto> positions, CancellationToken cancellationToken = default);
}
