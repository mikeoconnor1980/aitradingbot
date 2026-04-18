using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Helpers;
using TradePilot.Application.Trading.Signals.Models;

namespace TradePilot.Application.Trading.Signals.Implementations;

public sealed class StructureShiftSignal : IDerivedSignal
{
    public string Name => "structure_shift";

    public SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request)
    {
        var candles = context.GetCandles(request.Timeframe);
        var pivotBars = SignalParameterReader.GetInt(request.Parameters, "pivot_bars", 2);
        var directionRaw = SignalParameterReader.GetString(request.Parameters, "direction", "bullish");

        if (candles.Count < pivotBars * 2 + 10)
        {
            return SignalEvaluationResult.False("Not enough candles.");
        }

        var direction = directionRaw.Equals("bearish", StringComparison.OrdinalIgnoreCase)
            ? StructureShiftDirection.Bearish
            : StructureShiftDirection.Bullish;

        var current = candles[^1];
        var history = candles.Take(candles.Count - 1).ToList();

        if (direction == StructureShiftDirection.Bullish)
        {
            var recentPivotHigh = CandleMath.FindRecentPivotHigh(history, pivotBars);
            if (recentPivotHigh is null)
            {
                return SignalEvaluationResult.False("No pivot high found.");
            }

            var brokeAbove = current.Close > recentPivotHigh.Price;

            return brokeAbove
                ? SignalEvaluationResult.True(1m, new Dictionary<string, object?>
                {
                    ["direction"] = "bullish",
                    ["broken_pivot"] = recentPivotHigh.Price
                })
                : SignalEvaluationResult.False("No bullish structure shift.");
        }
        else
        {
            var recentPivotLow = CandleMath.FindRecentPivotLow(history, pivotBars);
            if (recentPivotLow is null)
            {
                return SignalEvaluationResult.False("No pivot low found.");
            }

            var brokeBelow = current.Close < recentPivotLow.Price;

            return brokeBelow
                ? SignalEvaluationResult.True(1m, new Dictionary<string, object?>
                {
                    ["direction"] = "bearish",
                    ["broken_pivot"] = recentPivotLow.Price
                })
                : SignalEvaluationResult.False("No bearish structure shift.");
        }
    }
}
