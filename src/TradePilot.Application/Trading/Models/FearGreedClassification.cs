namespace TradePilot.Application.Trading.Models;

/// <summary>
/// Classification buckets for the Crypto Fear &amp; Greed Index (0–100).
/// </summary>
public enum FearGreedClassification
{
    /// <summary>0–24: market participants are extremely fearful.</summary>
    ExtremeFear,

    /// <summary>25–49: elevated fear in the market.</summary>
    Fear,

    /// <summary>50: neither fearful nor greedy.</summary>
    Neutral,

    /// <summary>51–74: greed is driving the market.</summary>
    Greed,

    /// <summary>75–100: extreme greed, possible overheating.</summary>
    ExtremeGreed
}
