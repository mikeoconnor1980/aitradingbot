using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Agent.Models;

/// <summary>
/// Envelope carrying a real-time event from the Worker to the API for SignalR relay.
/// Only one payload property will be non-null per envelope.
/// </summary>
public sealed class SignalRRelayEnvelope
{
    public FillEventDto? Fill { get; init; }
    public OrderUpdateDto? OrderUpdate { get; init; }
    public ConnectionStatusDto? UserConnectionStatus { get; init; }
}
