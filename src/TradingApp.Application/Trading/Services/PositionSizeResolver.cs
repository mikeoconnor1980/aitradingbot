using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Trading.Services;

internal static class PositionSizeResolver
{
    public static decimal ResolveNotional(RiskConfig risk, decimal accountEquity, decimal? stopLossPercent = null)
    {
        ArgumentNullException.ThrowIfNull(risk);

        return risk.PositionSizeType switch
        {
            PositionSizeType.PercentWallet => Math.Max(0m, accountEquity) * (Math.Abs(risk.PositionSizeValue) / 100m),
            PositionSizeType.FixedNotional => Math.Abs(risk.PositionSizeValue),
            PositionSizeType.RiskBased => CalculateRiskBased(risk, accountEquity, stopLossPercent),
            _ => throw new ArgumentOutOfRangeException(nameof(risk), risk.PositionSizeType, "Unknown position size type")
        };
    }

    private static decimal CalculateRiskBased(RiskConfig risk, decimal accountEquity, decimal? stopLossPercent)
    {
        if (!risk.RiskPerTradePercent.HasValue || risk.RiskPerTradePercent.Value <= 0m)
        {
            return 0m;
        }

        if (!stopLossPercent.HasValue || stopLossPercent.Value <= 0m)
        {
            return 0m;
        }

        var equity = Math.Max(0m, accountEquity);
        var r = equity * (risk.RiskPerTradePercent.Value / 100m);

        return r / (stopLossPercent.Value / 100m);
    }
}