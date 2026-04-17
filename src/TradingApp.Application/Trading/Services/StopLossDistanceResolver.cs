using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Trading.Services;

internal static class StopLossDistanceResolver
{
    public static decimal? Resolve(
        ExitRuleConfig stopLossConfig,
        decimal? atr,
        decimal anchorPrice,
        decimal? gridBreakdownThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(stopLossConfig);

        if (stopLossConfig.Enabled)
        {
            var resolved = stopLossConfig.Type switch
            {
                ExitRuleType.FixedPercent when stopLossConfig.Value.HasValue && stopLossConfig.Value.Value > 0m
                    => stopLossConfig.Value.Value,

                ExitRuleType.AtrTrailing when atr.HasValue && atr.Value > 0m && anchorPrice > 0m
                    => (atr.Value * (stopLossConfig.AtrMultiplier ?? 3m)) / anchorPrice * 100m,

                ExitRuleType.AtrInitial when atr.HasValue && atr.Value > 0m && anchorPrice > 0m
                    => (atr.Value * (stopLossConfig.AtrMultiplier ?? 2m)) / anchorPrice * 100m,

                ExitRuleType.AtrInitial when stopLossConfig.Value.HasValue && stopLossConfig.Value.Value > 0m
                    => stopLossConfig.Value.Value,

                _ => (decimal?)null,
            };

            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        if (gridBreakdownThreshold.HasValue && gridBreakdownThreshold.Value > 0m)
        {
            return gridBreakdownThreshold.Value;
        }

        return null;
    }
}