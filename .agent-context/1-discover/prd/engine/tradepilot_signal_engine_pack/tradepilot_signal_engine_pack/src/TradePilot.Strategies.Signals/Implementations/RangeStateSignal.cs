using System;
using System.Collections.Generic;
using System.Linq;
using TradePilot.Strategies.Signals.Abstractions;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Implementations;

public sealed class RangeStateSignal : IDerivedSignal
{
    public string Name => "range_state";

    public SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request)
    {
        var candles = context.GetCandles(request.Timeframe);
        var lookbackBars = SignalParameterReader.GetInt(request.Parameters, "lookback_bars", 30);
        var minTouches = SignalParameterReader.GetInt(request.Parameters, "min_touches", 4);
        var maxSlopeAbsPercent = SignalParameterReader.GetDecimal(request.Parameters, "max_slope_abs_percent", 1.0m);
        var boundaryTolerancePercent = SignalParameterReader.GetDecimal(request.Parameters, "boundary_tolerance_percent", 0.003m);

        if (candles.Count < lookbackBars)
            return SignalEvaluationResult.False("Not enough candles.");

        var window = candles[^lookbackBars..];
        var highest = window.Max(x => x.High);
        var lowest = window.Min(x => x.Low);
        var firstClose = window[0].Close;
        var lastClose = window[^1].Close;

        if (firstClose == 0)
            return SignalEvaluationResult.False("Invalid price series.");

        var slopePercent = Math.Abs((lastClose - firstClose) / firstClose) * 100m;
        var touches = CandleMath.CountBoundaryTouches(window, lowest, highest, boundaryTolerancePercent);

        var isRange = slopePercent <= maxSlopeAbsPercent && touches >= minTouches;

        return isRange
            ? SignalEvaluationResult.True(
                1m,
                new Dictionary<string, object?>
                {
                    ["lower_bound"] = lowest,
                    ["upper_bound"] = highest,
                    ["touches"] = touches,
                    ["slope_percent"] = slopePercent
                })
            : SignalEvaluationResult.False("Range conditions not met.");
    }
}