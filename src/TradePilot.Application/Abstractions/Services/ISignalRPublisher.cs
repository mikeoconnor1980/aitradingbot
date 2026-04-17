using TradePilot.Application.Agent.Models;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

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

    Task BroadcastExecutionLogAsync(ExecutionLogDto log, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for broadcasting execution log entries via SignalR.
/// </summary>
public sealed class ExecutionLogDto
{
    public required string AgentId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string Category { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public Dictionary<string, object>? Data { get; init; }
}
