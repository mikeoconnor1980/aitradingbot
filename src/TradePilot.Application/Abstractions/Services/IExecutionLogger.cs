using TradePilot.Application.Agent.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Buffers structured execution log entries produced during strategy evaluation.
/// Entries are drained on each heartbeat and sent to the control plane.
/// </summary>
public interface IExecutionLogger
{
    /// <summary>Current logging level. Detail entries are only recorded when this is <see cref="ExecutionLogLevel.Detail"/>.</summary>
    ExecutionLogLevel CurrentLevel { get; }

    /// <summary>Enqueue a log entry. Detail-level entries are silently dropped when <see cref="CurrentLevel"/> is Summary.</summary>
    void Log(ExecutionLogEntry entry);

    /// <summary>Dequeue all buffered entries. Returns an empty list if nothing is buffered.</summary>
    IReadOnlyList<ExecutionLogEntry> Drain();
}

/// <summary>
/// Convenience extension methods for <see cref="IExecutionLogger"/>.
/// </summary>
public static class ExecutionLoggerExtensions
{
    public static void LogSummary(
        this IExecutionLogger logger,
        ExecutionLogCategory category,
        string message,
        Dictionary<string, object>? data = null)
    {
        logger.Log(new ExecutionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = category,
            Level = ExecutionLogLevel.Summary,
            Message = message,
            Data = data,
        });
    }

    public static void LogDetail(
        this IExecutionLogger logger,
        ExecutionLogCategory category,
        string message,
        Dictionary<string, object>? data = null)
    {
        logger.Log(new ExecutionLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Category = category,
            Level = ExecutionLogLevel.Detail,
            Message = message,
            Data = data,
        });
    }
}

/// <summary>
/// No-op execution logger used in backtesting and when execution logging is disabled.
/// </summary>
public sealed class NullExecutionLogger : IExecutionLogger
{
    public static readonly NullExecutionLogger Instance = new();

    private NullExecutionLogger() { }

    public ExecutionLogLevel CurrentLevel => ExecutionLogLevel.Summary;

    public void Log(ExecutionLogEntry entry) { }

    public IReadOnlyList<ExecutionLogEntry> Drain() => [];
}
