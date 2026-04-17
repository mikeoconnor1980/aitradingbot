namespace TradePilot.Indicators;

/// <summary>
/// Contains the nearest support and resistance levels identified by swing-point analysis.
/// </summary>
public sealed record SupportResistanceResult(decimal? Support, decimal? Resistance);
