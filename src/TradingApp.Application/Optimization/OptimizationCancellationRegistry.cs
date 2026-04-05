using System.Collections.Concurrent;

namespace TradingApp.Application.Optimization;

public sealed class OptimizationCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public CancellationTokenSource Register(Guid runId)
    {
        var cts = new CancellationTokenSource();
        _tokens[runId] = cts;
        return cts;
    }

    public bool TryCancel(Guid runId)
    {
        if (_tokens.TryRemove(runId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        return false;
    }

    public void Remove(Guid runId)
    {
        if (_tokens.TryRemove(runId, out var cts))
        {
            cts.Dispose();
        }
    }
}
