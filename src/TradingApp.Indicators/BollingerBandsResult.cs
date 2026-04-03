namespace TradingApp.Indicators;

/// <summary>
/// Bollinger Bands calculation result containing upper, middle, and lower band values.
/// </summary>
public sealed record BollingerBandsResult(decimal Upper, decimal Middle, decimal Lower);