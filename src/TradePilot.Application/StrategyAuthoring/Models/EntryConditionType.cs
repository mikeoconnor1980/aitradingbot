namespace TradePilot.Application.StrategyAuthoring.Models;

public enum EntryConditionType
{
    Unknown,
    Rsi,
    PriceVsEma,
    Macd,
    SupportResistance,
    CandlePattern,
    LiquiditySweep,
    StructureShift,
}