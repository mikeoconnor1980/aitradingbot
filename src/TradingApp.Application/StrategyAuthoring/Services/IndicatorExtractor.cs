using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

/// <summary>
/// Extracts required indicator computations from a strategy configuration.
/// </summary>
public static class IndicatorExtractor
{
    public static IReadOnlyList<IndicatorRequirement> Extract(StrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requirements = new List<IndicatorRequirement>();

        if (config.EntryConditions is null)
        {
            return requirements;
        }

        foreach (var condition in config.EntryConditions.Where(entry => entry.Enabled))
        {
            ExtractFromCondition(condition, seen, requirements);
        }

        return requirements;
    }

    private static void ExtractFromCondition(
        EntryConditionConfig condition,
        HashSet<string> seen,
        List<IndicatorRequirement> requirements)
    {
        switch (condition.Type)
        {
            case EntryConditionType.Rsi when condition.Params is RsiParams rsi:
                AddIfNew(seen, requirements, new IndicatorRequirement
                {
                    Type = "RSI",
                    Period = rsi.Period
                });
                break;

            case EntryConditionType.PriceVsEma when condition.Params is PriceVsEmaParams ema:
                AddIfNew(seen, requirements, new IndicatorRequirement
                {
                    Type = "EMA",
                    Period = ema.Period
                });
                break;

            case EntryConditionType.Macd when condition.Params is MacdParams macd:
                AddIfNew(seen, requirements, new IndicatorRequirement
                {
                    Type = "MACD",
                    FastPeriod = macd.FastPeriod,
                    SlowPeriod = macd.SlowPeriod,
                    SignalPeriod = macd.SignalPeriod
                });
                break;
        }
    }

    private static void AddIfNew(
        HashSet<string> seen,
        List<IndicatorRequirement> requirements,
        IndicatorRequirement requirement)
    {
        var key = $"{requirement.Type}:{requirement.Period}:{requirement.FastPeriod}:{requirement.SlowPeriod}:{requirement.SignalPeriod}";
        if (seen.Add(key))
        {
            requirements.Add(requirement);
        }
    }
}