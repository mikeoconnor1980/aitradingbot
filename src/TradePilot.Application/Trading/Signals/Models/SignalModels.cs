namespace TradePilot.Application.Trading.Signals.Models;

public sealed record SignalRequest(
    string Name,
    string Timeframe,
    IReadOnlyDictionary<string, object?> Parameters
);

public sealed record SignalEvaluationResult(
    bool IsMatch,
    decimal Score,
    IReadOnlyDictionary<string, object?> Metadata
)
{
    public static SignalEvaluationResult False(string reason)
        => new(false, 0m, new Dictionary<string, object?> { ["reason"] = reason });

    public static SignalEvaluationResult True(decimal score = 1m, IReadOnlyDictionary<string, object?>? metadata = null)
        => new(true, score, metadata ?? new Dictionary<string, object?>());
}

public enum SweepSide
{
    Upside,
    Downside
}

public enum StructureShiftDirection
{
    Bullish,
    Bearish
}

public sealed record PivotPoint(int Index, decimal Price);
