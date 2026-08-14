namespace TradePilot.Domain.Enums;

/// <summary>Final deterministic outcome of one strategy evaluation.</summary>
public enum StrategyDecision
{
    NoTrade,
    EnterLong,
    EnterShort,
    Exit,
    Hold,
    RejectedByRisk,
}
