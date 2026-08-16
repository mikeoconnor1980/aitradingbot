namespace TradePilot.Application.MarketAnalysis.Models;

/// <summary>
/// Represents deterministic technical facts for one market and one timeframe.
/// </summary>
/// <param name="Symbol">The requested exchange-facing market symbol.</param>
/// <param name="Timeframe">The single candle timeframe used for every calculation.</param>
/// <param name="Timestamp">The UTC close time of the latest completed candle used in the analysis.</param>
/// <param name="Price">The close price of the latest completed candle; this is not a live, mark, or index price.</param>
/// <param name="Indicators">Calculated indicator values and normalized distances.</param>
/// <param name="Trend">Strict close/EMA alignment classification.</param>
/// <param name="Momentum">RSI-based momentum classification.</param>
/// <param name="VolatilityRegime">ATR-percentage classification under TradePilot's initial policy.</param>
/// <param name="MarketStructure">Classification of the two most recent pairs of confirmed pivot swings.</param>
/// <param name="RecentSwingHigh">The latest confirmed pivot high, or <see langword="null"/> when unavailable.</param>
/// <param name="RecentSwingLow">The latest confirmed pivot low, or <see langword="null"/> when unavailable.</param>
public sealed record MarketAnalysisResult(
    string Symbol,
    string Timeframe,
    DateTimeOffset Timestamp,
    decimal Price,
    MarketIndicatorValues Indicators,
    MarketTrend Trend,
    MarketMomentum Momentum,
    VolatilityRegime VolatilityRegime,
    MarketStructure MarketStructure,
    decimal? RecentSwingHigh,
    decimal? RecentSwingLow);
