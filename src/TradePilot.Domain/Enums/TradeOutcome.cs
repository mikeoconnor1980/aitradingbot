namespace TradePilot.Domain.Enums;

/// <summary>Optional deterministic net-PnL filter for completed trades.</summary>
public enum TradeOutcome
{
    Winner,
    Loser,
    Breakeven,
}
