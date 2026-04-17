namespace TradePilot.Application.Agent.Models;

/// <summary>
/// A single execution log entry produced by the strategy engine during candle evaluation.
/// Buffered on the worker and drained with each heartbeat.
/// </summary>
public sealed record ExecutionLogEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }
    public required ExecutionLogCategory Category { get; init; }
    public required ExecutionLogLevel Level { get; init; }
    public required string Message { get; init; }

    /// <summary>
    /// Optional structured data (indicator values, gate results, etc.).
    /// Serialized as JSON for transport and persistence.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Categorizes the phase of strategy evaluation that produced the log entry.
/// </summary>
public enum ExecutionLogCategory
{
    CandleClose,
    EntryGate,
    ExitCheck,
    RiskEngine,
    Signal,
    GridState,
    Drawdown,
    Indicator,
}

/// <summary>
/// Controls logging granularity. Summary entries are always emitted;
/// Detail entries are emitted only when the execution logger level is set to Detail.
/// </summary>
public enum ExecutionLogLevel
{
    Summary,
    Detail,
}
