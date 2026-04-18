using System;
using System.Collections.Generic;
using TradePilot.Strategies.Signals.Abstractions;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Implementations;

public sealed class LiquiditySweepSignal : IDerivedSignal
{
    public string Name => "liquidity_sweep";

    public SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request)
    {
        var candles = context.GetCandles(request.Timeframe);
        var lookbackBars = SignalParameterReader.GetInt(request.Parameters, "lookback_bars", 50);
        var pivotBars = SignalParameterReader.GetInt(request.Parameters, "pivot_bars", 2);
        var sideRaw = SignalParameterReader.GetString(request.Parameters, "side", "upside");

        if (candles.Count < Math.Max(lookbackBars, pivotBars * 2 + 3))
            return SignalEvaluationResult.False("Not enough candles.");

        var side = sideRaw.Equals("downside", StringComparison.OrdinalIgnoreCase)
            ? SweepSide.Downside
            : SweepSide.Upside;

        var window = candles[^lookbackBars..];
        var current = window[^1];

        if (side == SweepSide.Upside)
        {
            var pivotHigh = CandleMath.FindRecentPivotHigh(window[..^1], pivotBars);
            if (pivotHigh is null)
                return SignalEvaluationResult.False("No valid pivot high.");

            var brokeAbove = current.High > pivotHigh.Price;
            var rejectedBackBelow = current.Close < pivotHigh.Price;

            return brokeAbove && rejectedBackBelow
                ? SignalEvaluationResult.True(
                    1m,
                    new Dictionary<string, object?>
                    {
                        ["side"] = "upside",
                        ["pivot_price"] = pivotHigh.Price,
                        ["current_high"] = current.High,
                        ["current_close"] = current.Close
                    })
                : SignalEvaluationResult.False("No upside liquidity sweep.");
        }
        else
        {
            var pivotLow = CandleMath.FindRecentPivotLow(window[..^1], pivotBars);
            if (pivotLow is null)
                return SignalEvaluationResult.False("No valid pivot low.");

            var brokeBelow = current.Low < pivotLow.Price;
            var rejectedBackAbove = current.Close > pivotLow.Price;

            return brokeBelow && rejectedBackAbove
                ? SignalEvaluationResult.True(
                    1m,
                    new Dictionary<string, object?>
                    {
                        ["side"] = "downside",
                        ["pivot_price"] = pivotLow.Price,
                        ["current_low"] = current.Low,
                        ["current_close"] = current.Close
                    })
                : SignalEvaluationResult.False("No downside liquidity sweep.");
        }
    }
}