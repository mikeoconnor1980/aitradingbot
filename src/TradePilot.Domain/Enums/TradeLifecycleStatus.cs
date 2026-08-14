namespace TradePilot.Domain.Enums;

/// <summary>Persisted lifecycle state of a logical trade.</summary>
public enum TradeLifecycleStatus
{
    Open,
    PartiallyClosed,
    Closed,
}
