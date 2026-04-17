using TradingApp.Application.Agent.Models;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Worker.Services;

/// <summary>
/// No-op publisher used when Azure SignalR is not configured.
/// Allows UserEventStreamService to run (for Telegram notifications)
/// even when the Worker can't push real-time updates to a browser.
/// </summary>
public sealed class NullSignalRPublisher : ISignalRPublisher
{
    public Task BroadcastPriceUpdateAsync(PriceUpdateDto update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastFillEventAsync(FillEventDto fill, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastOrderUpdateAsync(OrderUpdateDto orderUpdate, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastUserConnectionStatusAsync(ConnectionStatusDto status, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastOrdersResyncAsync(IReadOnlyList<OpenOrderDto> orders, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastPositionsResyncAsync(IReadOnlyList<PositionDto> positions, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task BroadcastExecutionLogAsync(ExecutionLogDto log, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
