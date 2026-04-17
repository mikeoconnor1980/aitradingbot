using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Trading.Services;

public sealed record DrawdownResult
{
    public required decimal NewHighWaterMark { get; init; }

    public required decimal DrawdownPercent { get; init; }

    public required decimal ScalingFactor { get; init; }

    public required bool IsHalted { get; init; }
}

public static class DrawdownEvaluator
{
    public static DrawdownResult Evaluate(
        decimal currentEquity,
        decimal highWaterMark,
        IReadOnlyList<DrawdownTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var sanitizedEquity = Math.Max(0m, currentEquity);
        var sanitizedHighWaterMark = Math.Max(0m, highWaterMark);
        var newHighWaterMark = Math.Max(sanitizedHighWaterMark, sanitizedEquity);
        var drawdownPercent = newHighWaterMark > 0m
            ? ((newHighWaterMark - sanitizedEquity) / newHighWaterMark) * 100m
            : 0m;

        var scalingFactor = 1.0m;
        for (var index = tiers.Count - 1; index >= 0; index--)
        {
            var tier = tiers[index];
            if (drawdownPercent >= tier.ThresholdPercent)
            {
                scalingFactor = tier.ScalingFactor;
                break;
            }
        }

        return new DrawdownResult
        {
            NewHighWaterMark = newHighWaterMark,
            DrawdownPercent = drawdownPercent,
            ScalingFactor = scalingFactor,
            IsHalted = scalingFactor == 0m,
        };
    }
}