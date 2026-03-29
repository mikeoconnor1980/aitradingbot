using System.Threading;
using TradingApp.Application.Backtesting.Services;

namespace TradingApp.Application.Backtesting;

public sealed class BacktestExecutionContextAccessor
{
    private readonly AsyncLocal<SimulatedExecutionEngine?> _currentExecutionEngine = new();
    private readonly AsyncLocal<long> _currentTimestampUtc = new();

    public SimulatedExecutionEngine? CurrentExecutionEngine
    {
        get => _currentExecutionEngine.Value;
        set => _currentExecutionEngine.Value = value;
    }

    public long CurrentTimestampUtc
    {
        get => _currentTimestampUtc.Value;
        set => _currentTimestampUtc.Value = value;
    }
}