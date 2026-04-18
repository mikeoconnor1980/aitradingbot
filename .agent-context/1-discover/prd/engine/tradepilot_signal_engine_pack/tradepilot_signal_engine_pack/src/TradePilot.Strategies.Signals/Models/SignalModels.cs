using System;
using System.Collections.Generic;

namespace TradePilot.Strategies.Signals.Models;

public sealed record Candle(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
)
{
    public decimal BodySize => Math.Abs(Close - Open);
    public decimal Range => High - Low;
    public bool IsBullish => Close > Open;
    public bool IsBearish => Close < Open;
    public decimal UpperWick => High - Math.Max(Open, Close);
    public decimal LowerWick => Math.Min(Open, Close) - Low;
}

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

public enum MarketRegime
{
    Unknown = 0,
    Ranging = 1,
    TrendingUp = 2,
    TrendingDown = 3,
    Breakout = 4,
    HighVolatility = 5,
    ParabolicUp = 6,
    ParabolicDown = 7
}

public sealed record PivotPoint(int Index, decimal Price);