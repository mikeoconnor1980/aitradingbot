using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Trading.Services;

internal static class PositionSizeResolver
{
    public static decimal ResolveNotional(RiskConfig risk, decimal accountEquity)
    {
        ArgumentNullException.ThrowIfNull(risk);

        return risk.PositionSizeType switch
        {
            PositionSizeType.PercentWallet => Math.Max(0m, accountEquity) * (Math.Abs(risk.PositionSizeValue) / 100m),
            PositionSizeType.FixedNotional => Math.Abs(risk.PositionSizeValue),
            _ => Math.Abs(risk.PositionSizeValue)
        };
    }
}