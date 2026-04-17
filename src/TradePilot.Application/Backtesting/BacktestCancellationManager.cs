using System.Collections.Concurrent;

namespace TradePilot.Application.Backtesting;

public sealed class BacktestCancellationManager
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationTokenSource Register(Guid backtestRunId)
    {
        var cts = new CancellationTokenSource();
        _sources[backtestRunId] = cts;
        return cts;
    }

    public bool TryCancel(Guid backtestRunId)
    {
        if (!_sources.TryRemove(backtestRunId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        cts.Dispose();
        return true;
    }

    public void Remove(Guid backtestRunId)
    {
        if (_sources.TryRemove(backtestRunId, out var cts))
        {
            cts.Dispose();
        }
    }
}
