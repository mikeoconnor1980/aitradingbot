namespace TradePilot.Application.Agent.Models;

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

    /// <summary>Agent executable version reported via heartbeat.</summary>
    public string? AgentVersion { get; set; }

    /// <summary>Current auto-update state reported by the agent.</summary>
    public UpdateState UpdateState { get; set; } = UpdateState.None;

    /// <summary>
    /// When set, the agent is killed and must not reconnect.
    /// If set to a future time, the kill takes effect at that time.
    /// </summary>
    public DateTimeOffset? KilledAtUtc { get; set; }

    /// <summary>Who or what triggered the kill switch (admin note).</summary>
    public string? KilledReason { get; set; }
}

public sealed class ActiveStrategyInfo
{
    public required string StrategyName { get; init; }
    public required string Market { get; init; }
    public required string Timeframe { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
}
