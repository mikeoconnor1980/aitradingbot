using TradePilot.Domain.Entities;

namespace TradePilot.Application.Trading.Signals;

public static class CandleExtensions
{
    public static decimal BodySize(this Candle candle) => Math.Abs(candle.Close - candle.Open);

    public static decimal Range(this Candle candle) => candle.High - candle.Low;

    public static bool IsBullish(this Candle candle) => candle.Close > candle.Open;

    public static bool IsBearish(this Candle candle) => candle.Close < candle.Open;

    public static decimal UpperWick(this Candle candle) => candle.High - Math.Max(candle.Open, candle.Close);

    public static decimal LowerWick(this Candle candle) => Math.Min(candle.Open, candle.Close) - candle.Low;
}
