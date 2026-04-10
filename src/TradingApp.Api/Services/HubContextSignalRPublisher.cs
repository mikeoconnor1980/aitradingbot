using Microsoft.AspNetCore.SignalR;
using TradingApp.Api.Hubs;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Services;

/// <summary>
/// In-process SignalR publisher that delegates to IHubContext&lt;MarketDataHub&gt;.
/// Used by the API process for backtest/optimization progress broadcasting.
/// </summary>
public sealed class HubContextSignalRPublisher : ISignalRPublisher
{
    private readonly IHubContext<MarketDataHub> _hubContext;

    public HubContextSignalRPublisher(IHubContext<MarketDataHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastPriceUpdateAsync(PriceUpdateDto update, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceivePriceUpdate", update, cancellationToken);

    public Task BroadcastConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceiveConnectionStatus", status, cancellationToken);

    public Task BroadcastFillEventAsync(FillEventDto fill, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceiveFillEvent", fill, cancellationToken);

    public Task BroadcastOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceiveOrderUpdate", orderUpdate, cancellationToken);

    public Task BroadcastUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceiveUserConnectionStatus", status, cancellationToken);

    public Task BroadcastOrdersResyncAsync(IReadOnlyList<OpenOrderDto> orders, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceiveOrdersResync", orders, cancellationToken);

    public Task BroadcastPositionsResyncAsync(IReadOnlyList<PositionDto> positions, CancellationToken cancellationToken = default)
        => _hubContext.Clients.All.SendAsync("ReceivePositionsResync", positions, cancellationToken);
}
