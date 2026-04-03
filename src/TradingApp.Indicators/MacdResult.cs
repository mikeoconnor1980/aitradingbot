namespace TradingApp.Indicators;

/// <summary>
/// MACD calculation result containing line, signal, and histogram values.
/// </summary>
public sealed record MacdResult(decimal Line, decimal Signal, decimal Histogram);