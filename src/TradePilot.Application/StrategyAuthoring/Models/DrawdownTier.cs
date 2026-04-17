namespace TradePilot.Application.StrategyAuthoring.Models;

/// <summary>
/// A single drawdown tier defining a threshold and its risk scaling factor.
/// </summary>
public sealed record DrawdownTier
{
    /// <summary>Drawdown percentage threshold (e.g. 5 = 5% drawdown from HWM).</summary>
    public decimal ThresholdPercent { get; init; }

    /// <summary>Scaling factor applied to base risk (0.0-1.0). 0.0 = halt all entries.</summary>
    public decimal ScalingFactor { get; init; }
}