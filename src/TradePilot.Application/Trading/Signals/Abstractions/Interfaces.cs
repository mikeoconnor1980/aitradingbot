using TradePilot.Application.Trading.Signals.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Trading.Signals.Abstractions;

public interface IDerivedSignal
{
    string Name { get; }
    SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request);
}

public interface ISignalContext
{
    string Symbol { get; }
    IReadOnlyDictionary<string, IReadOnlyList<Candle>> CandlesByTimeframe { get; }

    IReadOnlyList<Candle> GetCandles(string timeframe);
    Candle GetCurrentCandle(string timeframe);
    Candle? GetPreviousCandle(string timeframe, int offset = 1);

    decimal? GetIndicatorValue(string indicatorId);
    T? GetState<T>(string key) where T : class;
}

public interface IDerivedSignalRegistry
{
    void Register(IDerivedSignal signal);
    IDerivedSignal Get(string name);
    bool TryGet(string name, out IDerivedSignal? signal);
    IReadOnlyCollection<string> ListNames();
}
