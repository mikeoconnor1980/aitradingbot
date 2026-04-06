using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Agent.Models;

/// <summary>
/// Heartbeat payload sent from the Worker to the API on each check-in.
/// </summary>
public sealed class AgentHeartbeat
{
    public required string AgentId { get; init; }
    public required AgentState State { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string? WalletAddress { get; init; }
    public ActiveStrategyInfo? ActiveStrategy { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Results from order commands completed since the last heartbeat.
    /// </summary>
    public IReadOnlyList<OrderCommandResult> OrderResults { get; init; } = [];
}

/// <summary>
/// Response to a heartbeat, containing any pending commands for the agent.
/// </summary>
public sealed class HeartbeatResponse
{
    public IReadOnlyList<AgentCommand> PendingCommands { get; init; } = [];

    /// <summary>
    /// When true, the agent must stop all activity and shut down.
    /// </summary>
    public bool MustShutdown { get; init; }

    /// <summary>Reason shown to the agent operator.</summary>
    public string? ShutdownReason { get; init; }
}
