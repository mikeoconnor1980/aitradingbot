namespace TradePilot.Domain.Enums;

/// <summary>Deterministic source that caused a logical trade to close.</summary>
public enum TradeExitReason
{
    Unknown,
    TakeProfit,
    StopLoss,
    TrailingStop,
    Liquidation,
    Manual,
    RiskControl,
    External,
}
