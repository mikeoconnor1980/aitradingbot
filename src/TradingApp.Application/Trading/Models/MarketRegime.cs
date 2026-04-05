namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Strategy operating mode derived from market conditions.
/// Maps LLM context (or synthetic regime signals) to concrete strategy behaviour.
/// </summary>
public enum MarketRegime
{
    /// <summary>Favourable conditions — full grid, tighter spacing.</summary>
    Aggressive,

    /// <summary>Neutral conditions — standard parameters.</summary>
    Normal,

    /// <summary>Unfavourable conditions — wider spacing, reduced size.</summary>
    Defensive,

    /// <summary>High risk — no new grid deployments.</summary>
    RiskOff
}
