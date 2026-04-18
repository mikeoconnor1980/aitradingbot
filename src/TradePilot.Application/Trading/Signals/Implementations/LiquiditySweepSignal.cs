using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Helpers;
using TradePilot.Application.Trading.Signals.Models;

namespace TradePilot.Application.Trading.Signals.Implementations;

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
        {
            return SignalEvaluationResult.False("Not enough candles.");
        }

        var side = sideRaw.Equals("downside", StringComparison.OrdinalIgnoreCase)
            ? SweepSide.Downside
            : SweepSide.Upside;

        var window = candles.Skip(Math.Max(0, candles.Count - lookbackBars)).ToList();
        var current = window[^1];

        if (side == SweepSide.Upside)
        {
            var history = window.Take(window.Count - 1).ToList();
            var pivotHigh = CandleMath.FindRecentPivotHigh(history, pivotBars);
            if (pivotHigh is null)
            {
                return SignalEvaluationResult.False("No valid pivot high.");
            }

            var brokeAbove = current.High > pivotHigh.Price;
            var rejectedBackBelow = current.Close < pivotHigh.Price;

            return brokeAbove && rejectedBackBelow
                ? SignalEvaluationResult.True(1m, new Dictionary<string, object?>
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
            var history = window.Take(window.Count - 1).ToList();
            var pivotLow = CandleMath.FindRecentPivotLow(history, pivotBars);
            if (pivotLow is null)
            {
                return SignalEvaluationResult.False("No valid pivot low.");
            }

            var brokeBelow = current.Low < pivotLow.Price;
            var rejectedBackAbove = current.Close > pivotLow.Price;

            return brokeBelow && rejectedBackAbove
                ? SignalEvaluationResult.True(1m, new Dictionary<string, object?>
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
