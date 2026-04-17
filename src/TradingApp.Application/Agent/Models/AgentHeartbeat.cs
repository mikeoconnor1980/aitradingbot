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

    /// <summary>Semantic version of the agent executable (e.g. "0.1.0").</summary>
    public string? AgentVersion { get; init; }

    /// <summary>Current auto-update state reported by the agent.</summary>
    public UpdateState UpdateState { get; init; } = UpdateState.None;

    /// <summary>Reason the update was deferred (e.g. active trading session).</summary>
    public string? UpdateDeferredReason { get; init; }

    /// <summary>
    /// Results from order commands completed since the last heartbeat.
    /// </summary>
    public IReadOnlyList<OrderCommandResult> OrderResults { get; init; } = [];

    /// <summary>
    /// Execution log entries produced during strategy evaluation since the last heartbeat.
    /// </summary>
    public IReadOnlyList<ExecutionLogEntry> ExecutionLogs { get; init; } = [];
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

    /// <summary>True when a newer agent version is available.</summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>The latest available agent version (semver).</summary>
    public string? LatestVersion { get; init; }

    /// <summary>URL to download the installer for the latest version.</summary>
    public string? UpdateDownloadUrl { get; init; }

    /// <summary>SHA256 hash of the installer binary for verification.</summary>
    public string? UpdateSha256Hash { get; init; }

    /// <summary>
    /// Exchange network configuration pushed from the control plane.
    /// The agent uses these endpoints instead of its local config.
    /// </summary>
    public NetworkConfig? NetworkConfig { get; init; }

    /// <summary>
    /// Notification configuration pushed from the control plane (e.g. Telegram chat ID).
    /// </summary>
    public NotificationConfig? NotificationConfig { get; init; }
}

/// <summary>
/// Exchange network endpoints pushed from the API to the agent.
/// </summary>
public sealed class NetworkConfig
{
    /// <summary>REST API base URL (e.g. https://api.hyperliquid.xyz).</summary>
    public required string BaseUrl { get; init; }

    /// <summary>WebSocket base URL (e.g. wss://api.hyperliquid.xyz/ws).</summary>
    public required string WsBaseUrl { get; init; }

    /// <summary>Network identifier: "mainnet" or "testnet".</summary>
    public required string Network { get; init; }
}

public enum UpdateState
{
    None,
    Downloading,
    Applying,
    Failed,
    Deferred,
}

/// <summary>
/// Notification settings pushed from the API to the agent.
/// </summary>
public sealed class NotificationConfig
{
    /// <summary>User's linked Telegram chat ID (null if not linked).</summary>
    public long? TelegramChatId { get; init; }

    /// <summary>Telegram Bot API token for sending notifications.</summary>
    public string? TelegramBotToken { get; init; }
}
