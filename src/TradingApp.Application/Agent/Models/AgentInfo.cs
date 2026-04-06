namespace TradingApp.Application.Agent.Models;

/// <summary>
/// Represents a connected execution agent (Worker) and its current state.
/// </summary>
public sealed class AgentInfo
{
    public required string AgentId { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public AgentState State { get; set; } = AgentState.Idle;
    public DateTimeOffset LastHeartbeat { get; set; }
    public DateTimeOffset ConnectedSince { get; init; }
    public string? WalletAddress { get; set; }
    public ActiveStrategyInfo? ActiveStrategy { get; set; }
    public string? LastError { get; set; }
}

public sealed class ActiveStrategyInfo
{
    public required string StrategyName { get; init; }
    public required string Market { get; init; }
    public required string Timeframe { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
}
