using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Agent.Models;

/// <summary>
/// A command queued by the dashboard for a specific agent to pick up.
/// </summary>
public sealed class AgentCommand
{
    public required string CommandId { get; init; }
    public required string AgentId { get; init; }
    public required AgentCommandType Type { get; init; }
    public StrategyConfig? StrategyConfig { get; init; }
    public OrderCommandPayload? OrderPayload { get; init; }
    public ClosePositionPayload? ClosePositionPayload { get; init; }
    public CancelOrderPayload? CancelPayload { get; init; }
    public CancelAllOrdersPayload? CancelAllPayload { get; init; }
    public SetLeveragePayload? LeveragePayload { get; init; }
    public TriggerOrderPayload? TriggerPayload { get; init; }
    public ModifyTriggerOrderPayload? ModifyTriggerPayload { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public enum AgentCommandType
{
    Start,
    Stop,
    PlaceOrder,
    ClosePosition,
    CancelOrder,
    CancelAllOrders,
    SetLeverage,
    PlaceTriggerOrder,
    ModifyTriggerOrder,
    CancelTriggerOrder,
}
