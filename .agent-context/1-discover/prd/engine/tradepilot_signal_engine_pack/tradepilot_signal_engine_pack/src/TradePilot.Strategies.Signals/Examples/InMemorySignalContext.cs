using System;
using System.Collections.Generic;
using TradePilot.Strategies.Signals.Abstractions;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Examples;

public sealed class InMemorySignalContext : ISignalContext
{
    private readonly IReadOnlyDictionary<string, decimal?> _indicatorValues;
    private readonly IReadOnlyDictionary<string, object> _state;

    public InMemorySignalContext(
        string symbol,
        IReadOnlyDictionary<string, IReadOnlyList<Candle>> candlesByTimeframe,
        IReadOnlyDictionary<string, decimal?>? indicatorValues = null,
        IReadOnlyDictionary<string, object>? state = null)
    {
        Symbol = symbol;
        CandlesByTimeframe = candlesByTimeframe;
        _indicatorValues = indicatorValues ?? new Dictionary<string, decimal?>();
        _state = state ?? new Dictionary<string, object>();
    }

    public string Symbol { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<Candle>> CandlesByTimeframe { get; }

    public IReadOnlyList<Candle> GetCandles(string timeframe)
    {
        if (!CandlesByTimeframe.TryGetValue(timeframe, out var candles))
            throw new KeyNotFoundException($"No candles loaded for timeframe '{timeframe}'.");

        return candles;
    }

    public Candle GetCurrentCandle(string timeframe)
    {
        var candles = GetCandles(timeframe);
        if (candles.Count == 0) throw new InvalidOperationException("No candles available.");
        return candles[^1];
    }

    public Candle? GetPreviousCandle(string timeframe, int offset = 1)
    {
        var candles = GetCandles(timeframe);
        var index = candles.Count - 1 - offset;
        return index >= 0 ? candles[index] : null;
    }

    public decimal? GetIndicatorValue(string indicatorId)
        => _indicatorValues.TryGetValue(indicatorId, out var value) ? value : null;

    public T? GetState<T>(string key) where T : class
        => _state.TryGetValue(key, out var value) ? value as T : null;
}