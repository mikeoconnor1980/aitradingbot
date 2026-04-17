using System.Collections.Concurrent;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent.Models;

namespace TradePilot.Worker.Services;

/// <summary>
/// Thread-safe execution logger backed by a <see cref="ConcurrentQueue{T}"/>.
/// Entries are drained by <see cref="AgentCheckInService"/> on each heartbeat.
/// </summary>
public sealed class LiveExecutionLogger : IExecutionLogger
{
    private readonly ConcurrentQueue<ExecutionLogEntry> _entries = new();

    public ExecutionLogLevel CurrentLevel { get; set; } = ExecutionLogLevel.Summary;

    public void Log(ExecutionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Level == ExecutionLogLevel.Detail && CurrentLevel != ExecutionLogLevel.Detail)
        {
            return;
        }

        _entries.Enqueue(entry);
    }

    public IReadOnlyList<ExecutionLogEntry> Drain()
    {
        var drained = new List<ExecutionLogEntry>();
        while (_entries.TryDequeue(out var entry))
        {
            drained.Add(entry);
        }

        return drained;
    }
}
