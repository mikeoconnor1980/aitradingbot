namespace TradingApp.Application.Agent.Models;

/// <summary>
/// Transport model for order commands routed from the dashboard through the
/// control plane to a Worker agent. String-based to match Hyperliquid API conventions.
/// </summary>
public sealed class OrderCommandPayload
{
    public required string Asset { get; init; }
    public required string Side { get; init; }
    public required string OrderType { get; init; }
    public decimal? Price { get; init; }
    public required decimal Size { get; init; }
    public decimal? StopLossPrice { get; init; }
    public decimal? TakeProfitPrice { get; init; }
}

/// <summary>
/// Payload for cancelling a single order.
/// </summary>
public sealed class CancelOrderPayload
{
    public required string OrderId { get; init; }
    public required string Asset { get; init; }
}

/// <summary>
/// Payload for cancelling all orders for an asset.
/// </summary>
public sealed class CancelAllOrdersPayload
{
    public required string Asset { get; init; }
}

/// <summary>
/// Payload for setting leverage on an asset.
/// </summary>
public sealed class SetLeveragePayload
{
    public required string Asset { get; init; }
    public required int Leverage { get; init; }
    public bool IsCross { get; init; } = false;
}

/// <summary>
/// Payload for placing a trigger (SL/TP) order.
/// </summary>
public sealed class TriggerOrderPayload
{
    public required string Asset { get; init; }
    public required string Side { get; init; }
    public required decimal Size { get; init; }
    public required decimal TriggerPrice { get; init; }
    public required string TpslType { get; init; }
}

/// <summary>
/// Payload for modifying an existing trigger order.
/// </summary>
public sealed class ModifyTriggerOrderPayload
{
    public required string OrderId { get; init; }
    public required string Asset { get; init; }
    public required string Side { get; init; }
    public required decimal TriggerPrice { get; init; }
    public required decimal Size { get; init; }
    public required string TpslType { get; init; }
}

/// <summary>
/// Result of a completed order command, reported back via heartbeat.
/// </summary>
public sealed class OrderCommandResult
{
    public required string CommandId { get; init; }
    public required bool Success { get; init; }
    public string? OrderId { get; init; }
    public string? Detail { get; init; }
}
