using TradePilot.Application.Trading.Signals.Abstractions;
using TradePilot.Application.Trading.Signals.Helpers;
using TradePilot.Application.Trading.Signals.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Trading.Signals.Implementations;

public sealed class CandlePatternSignal : IDerivedSignal
{
    public string Name => "candle_pattern";

    public SignalEvaluationResult Evaluate(ISignalContext context, SignalRequest request)
    {
        var candles = context.GetCandles(request.Timeframe);
        if (candles.Count < 2)
        {
            return SignalEvaluationResult.False("Not enough candles.");
        }

        var pattern = SignalParameterReader.GetString(request.Parameters, "pattern", string.Empty);
        var current = candles[^1];
        var previous = candles[^2];

        return pattern switch
        {
            "bullish_engulfing" => BullishEngulfing(current, previous),
            "bearish_engulfing" => BearishEngulfing(current, previous),
            "bullish_rejection" => BullishRejection(current),
            "bearish_rejection" => BearishRejection(current),
            "bullish_rejection_or_engulfing" => Any(BullishRejection(current), BullishEngulfing(current, previous)),
            "bearish_rejection_or_engulfing" => Any(BearishRejection(current), BearishEngulfing(current, previous)),
            "bullish_continuation" => BullishContinuation(current),
            "bearish_continuation" => BearishContinuation(current),
            _ => SignalEvaluationResult.False($"Unknown candle pattern '{pattern}'.")
        };
    }

    private static SignalEvaluationResult BullishEngulfing(Candle current, Candle previous)
    {
        var match =
            previous.IsBearish() &&
            current.IsBullish() &&
            current.Open <= previous.Close &&
            current.Close >= previous.Open &&
            current.BodySize() > previous.BodySize();

        return match
            ? SignalEvaluationResult.True(1m, new Dictionary<string, object?> { ["pattern"] = "bullish_engulfing" })
            : SignalEvaluationResult.False("No bullish engulfing.");
    }

    private static SignalEvaluationResult BearishEngulfing(Candle current, Candle previous)
    {
        var match =
            previous.IsBullish() &&
            current.IsBearish() &&
            current.Open >= previous.Close &&
            current.Close <= previous.Open &&
            current.BodySize() > previous.BodySize();

        return match
            ? SignalEvaluationResult.True(1m, new Dictionary<string, object?> { ["pattern"] = "bearish_engulfing" })
            : SignalEvaluationResult.False("No bearish engulfing.");
    }

    private static SignalEvaluationResult BullishRejection(Candle current)
    {
        var match =
            current.IsBullish() &&
            current.Range() > 0 &&
            current.LowerWick() >= current.BodySize() * 1.5m &&
            current.Close > current.Open;

        return match
            ? SignalEvaluationResult.True(0.8m, new Dictionary<string, object?> { ["pattern"] = "bullish_rejection" })
            : SignalEvaluationResult.False("No bullish rejection.");
    }

    private static SignalEvaluationResult BearishRejection(Candle current)
    {
        var match =
            current.IsBearish() &&
            current.Range() > 0 &&
            current.UpperWick() >= current.BodySize() * 1.5m &&
            current.Close < current.Open;

        return match
            ? SignalEvaluationResult.True(0.8m, new Dictionary<string, object?> { ["pattern"] = "bearish_rejection" })
            : SignalEvaluationResult.False("No bearish rejection.");
    }

    private static SignalEvaluationResult BullishContinuation(Candle current)
    {
        var match = current.IsBullish() && current.BodySize() > 0 && current.UpperWick() <= current.BodySize();
        return match
            ? SignalEvaluationResult.True(0.7m, new Dictionary<string, object?> { ["pattern"] = "bullish_continuation" })
            : SignalEvaluationResult.False("No bullish continuation.");
    }

    private static SignalEvaluationResult BearishContinuation(Candle current)
    {
        var match = current.IsBearish() && current.BodySize() > 0 && current.LowerWick() <= current.BodySize();
        return match
            ? SignalEvaluationResult.True(0.7m, new Dictionary<string, object?> { ["pattern"] = "bearish_continuation" })
            : SignalEvaluationResult.False("No bearish continuation.");
    }

    private static SignalEvaluationResult Any(SignalEvaluationResult a, SignalEvaluationResult b)
        => a.IsMatch ? a : b;
}
