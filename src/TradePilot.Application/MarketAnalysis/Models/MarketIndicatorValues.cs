namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Contains indicators calculated from completed candles in chronological order.
/// </summary>
/// <param name="Ema20">The 20-period exponential moving average of candle closes.</param>
/// <param name="Ema50">The 50-period exponential moving average of candle closes.</param>
/// <param name="Ema200">The 200-period exponential moving average of candle closes.</param>
/// <param name="Rsi">The 14-period Wilder relative strength index.</param>
/// <param name="Atr">The 14-period Wilder average true range in price units.</param>
/// <param name="AtrPercent">ATR divided by the analysed close, expressed as a percentage.</param>
/// <param name="DistanceFromEma20Percent">The close minus EMA20, divided by EMA20, expressed as a percentage.</param>
/// <param name="DistanceFromEma50Percent">The close minus EMA50, divided by EMA50, expressed as a percentage.</param>
/// <param name="DistanceFromEma200Percent">The close minus EMA200, divided by EMA200, expressed as a percentage.</param>
public sealed record MarketIndicatorValues(
    decimal Ema20,
    decimal Ema50,
    decimal Ema200,
    decimal Rsi,
    decimal Atr,
    decimal AtrPercent,
    decimal DistanceFromEma20Percent,
    decimal DistanceFromEma50Percent,
    decimal DistanceFromEma200Percent);
