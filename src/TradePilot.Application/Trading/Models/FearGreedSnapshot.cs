namespace TradePilot.Application.Trading.Models;

/// <summary>
/// Point-in-time snapshot of the Crypto Fear &amp; Greed Index
/// attached to <see cref="MarketContext"/> for regime derivation.
/// </summary>
public sealed record FearGreedSnapshot(
    int Value,
    FearGreedClassification Classification,
    long TimestampUtc)
{
    public static FearGreedClassification Classify(int value) => value switch
    {
        <= 24 => FearGreedClassification.ExtremeFear,
        <= 49 => FearGreedClassification.Fear,
        50 => FearGreedClassification.Neutral,
        <= 74 => FearGreedClassification.Greed,
        _ => FearGreedClassification.ExtremeGreed
    };
}
