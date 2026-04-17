using System.Globalization;
using TradePilot.Application.Optimization.Models;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Optimization.Services;

public interface IStrategyConfigGenerator
{
    IReadOnlyList<GeneratedStrategy> Generate(string symbol, ParameterBounds bounds, int sampleSize, int? seed = null);
}

public sealed record GeneratedStrategy(StrategyConfig Config, string Description);

public sealed class StrategyConfigGenerator : IStrategyConfigGenerator
{
    private static readonly SignalTemplate[] Templates =
    [
        new([EntryConditionType.Rsi], EntryLogic.All),
        new([EntryConditionType.Macd], EntryLogic.All),
        new([EntryConditionType.PriceVsEma], EntryLogic.All),
        new([EntryConditionType.Rsi, EntryConditionType.Macd], EntryLogic.All),
        new([EntryConditionType.Rsi, EntryConditionType.Macd], EntryLogic.Any),
        new([EntryConditionType.Rsi, EntryConditionType.PriceVsEma], EntryLogic.All),
        new([EntryConditionType.Rsi, EntryConditionType.PriceVsEma], EntryLogic.Any),
        new([EntryConditionType.Macd, EntryConditionType.PriceVsEma], EntryLogic.All),
        new([EntryConditionType.Macd, EntryConditionType.PriceVsEma], EntryLogic.Any),
        new([EntryConditionType.Rsi, EntryConditionType.Macd, EntryConditionType.PriceVsEma], EntryLogic.All),
        new([EntryConditionType.Rsi, EntryConditionType.Macd, EntryConditionType.PriceVsEma], EntryLogic.Any),
    ];

    public IReadOnlyList<GeneratedStrategy> Generate(string symbol, ParameterBounds bounds, int sampleSize, int? seed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleSize);

        ValidateBounds(bounds);

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var results = new List<GeneratedStrategy>(sampleSize);

        for (var sampleIndex = 0; sampleIndex < sampleSize; sampleIndex++)
        {
            var template = Templates[rng.Next(Templates.Length)];
            var direction = NextFrom(bounds.Directions, rng);
            var timeframe = NextFrom(bounds.Timeframes, rng);
            var conditions = GenerateEntryConditions(template, bounds, rng, sampleIndex, direction);
            var exit = GenerateExitConfig(bounds, rng);
            var risk = GenerateRiskConfig(bounds, rng);
            var trendFilter = GenerateTrendFilter(bounds, rng, direction);

            var config = new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Signal,
                StrategyName = $"Optimizer-{sampleIndex + 1}",
                Exchange = "Hyperliquid",
                Market = symbol,
                Timeframe = timeframe,
                Direction = direction,
                Enabled = true,
                TemplateId = "custom_signal",
                EntryLogic = template.EntryLogic,
                EntryConditions = conditions,
                TrendFilter = trendFilter,
                Exit = exit,
                Risk = risk,
            };

            results.Add(new GeneratedStrategy(config, BuildDescription(conditions, template.EntryLogic, exit, risk, trendFilter, direction)));
        }

        return results;
    }

    private static IReadOnlyList<EntryConditionConfig> GenerateEntryConditions(
        SignalTemplate template,
        ParameterBounds bounds,
        Random rng,
        int sampleIndex,
        Direction direction)
    {
        var conditions = new List<EntryConditionConfig>(template.ConditionTypes.Count);

        for (var conditionIndex = 0; conditionIndex < template.ConditionTypes.Count; conditionIndex++)
        {
            var conditionType = template.ConditionTypes[conditionIndex];
            var conditionId = $"opt-{sampleIndex + 1}-{conditionIndex + 1}-{conditionType.ToString().ToLowerInvariant()}";

            conditions.Add(conditionType switch
            {
                EntryConditionType.Rsi => GenerateRsiCondition(conditionId, bounds, rng, direction),
                EntryConditionType.Macd => GenerateMacdCondition(conditionId, bounds, rng, direction),
                EntryConditionType.PriceVsEma => GeneratePriceVsEmaCondition(conditionId, bounds, rng),
                _ => throw new InvalidOperationException($"Unsupported optimizer entry condition type: {conditionType}.")
            });
        }

        return conditions;
    }

    private static EntryConditionConfig GenerateRsiCondition(string conditionId, ParameterBounds bounds, Random rng, Direction direction)
    {
        var rsiOperator = NextFrom(bounds.RsiOperators, rng);

        // Adjust threshold based on operator semantics and direction
        var threshold = NextFrom(bounds.RsiThresholds, rng);
        var label = rsiOperator switch
        {
            "gt" or "gte" or "cross_above" => "RSI Overbought",
            _ => "RSI Oversold"
        };

        // For short direction with "lt" operators, flip to overbought territory
        if (direction == Direction.Short && rsiOperator is "lt" or "lte")
        {
            threshold = 100m - threshold;
            rsiOperator = rsiOperator == "lt" ? "gt" : "gte";
            label = "RSI Overbought";
        }

        return new EntryConditionConfig
        {
            Id = conditionId,
            Enabled = true,
            Type = EntryConditionType.Rsi,
            Label = label,
            Params = new RsiParams
            {
                Period = NextFrom(bounds.RsiPeriods, rng),
                Operator = rsiOperator,
                Value = threshold,
            },
        };
    }

    private static EntryConditionConfig GenerateMacdCondition(string conditionId, ParameterBounds bounds, Random rng, Direction direction)
    {
        var macdParams = CreateMacdParams(bounds, rng, direction);

        var label = macdParams.Operator switch
        {
            "cross_above_signal" => "MACD Bullish Cross",
            "cross_below_signal" => "MACD Bearish Cross",
            "above_zero" => "MACD Above Zero",
            "below_zero" => "MACD Below Zero",
            "histogram_rising" => "MACD Histogram Rising",
            "histogram_falling" => "MACD Histogram Falling",
            _ => "MACD Signal"
        };

        return new EntryConditionConfig
        {
            Id = conditionId,
            Enabled = true,
            Type = EntryConditionType.Macd,
            Label = label,
            Params = macdParams,
        };
    }

    private static EntryConditionConfig GeneratePriceVsEmaCondition(string conditionId, ParameterBounds bounds, Random rng)
    {
        var emaOperator = NextFrom(bounds.PriceVsEmaOperators, rng);
        var period = NextFrom(bounds.EmaPeriods, rng);

        var label = emaOperator switch
        {
            "near" => $"Price Near EMA({period})",
            "above" => $"Price Above EMA({period})",
            "below" => $"Price Below EMA({period})",
            "cross_above" => $"Price Cross Above EMA({period})",
            "cross_below" => $"Price Cross Below EMA({period})",
            _ => $"PriceVsEma({period})"
        };

        // Only "near" uses distance parameters
        var distanceType = emaOperator == "near" ? "percent" : "percent";
        var distanceValue = emaOperator == "near" ? NextFrom(bounds.EmaProximityPercents, rng) : 0m;

        return new EntryConditionConfig
        {
            Id = conditionId,
            Enabled = true,
            Type = EntryConditionType.PriceVsEma,
            Label = label,
            Params = new PriceVsEmaParams
            {
                Period = period,
                Operator = emaOperator,
                DistanceType = distanceType,
                DistanceValue = distanceValue,
            },
        };
    }

    private static MacdParams CreateMacdParams(ParameterBounds bounds, Random rng, Direction direction)
    {
        var macdOperator = NextFrom(bounds.MacdOperators, rng);

        // For short direction, flip bullish→bearish operators
        if (direction == Direction.Short)
        {
            macdOperator = macdOperator switch
            {
                "cross_above_signal" => "cross_below_signal",
                "above_zero" => "below_zero",
                "histogram_rising" => "histogram_falling",
                _ => macdOperator
            };
        }

        var attempts = 0;

        while (true)
        {
            var fast = NextFrom(bounds.MacdFastPeriods, rng);
            var slow = NextFrom(bounds.MacdSlowPeriods, rng);
            var signal = NextFrom(bounds.MacdSignalPeriods, rng);

            if (fast < slow)
            {
                return new MacdParams
                {
                    FastPeriod = fast,
                    SlowPeriod = slow,
                    SignalPeriod = signal,
                    Operator = macdOperator,
                };
            }

            attempts++;
            if (attempts > 100)
            {
                throw new InvalidOperationException("Unable to generate valid MACD parameters from configured bounds.");
            }
        }
    }

    private static ExitConfig GenerateExitConfig(ParameterBounds bounds, Random rng)
    {
        var stopLossType = NextFrom(bounds.StopLossTypes, rng);

        var stopLoss = stopLossType switch
        {
            ExitRuleType.AtrInitial => new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.AtrInitial,
                AtrMultiplier = NextFrom(bounds.AtrMultiplierOptions, rng),
                AtrPeriod = NextFrom(bounds.AtrPeriodOptions, rng),
            },
            ExitRuleType.FixedPercent => new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.FixedPercent,
                Value = NextFromRange(bounds.StopLossMin, bounds.StopLossMax, bounds.StopLossStep, rng),
            },
            _ => throw new InvalidOperationException($"Unsupported optimizer stop-loss type: {stopLossType}.")
        };

        return new ExitConfig
        {
            TakeProfit = new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.FixedPercent,
                Value = NextFromRange(bounds.TakeProfitMin, bounds.TakeProfitMax, bounds.TakeProfitStep, rng),
            },
            StopLoss = stopLoss,
            ExitOnOppositeSignal = NextFrom(bounds.ExitOnOppositeSignalOptions, rng),
        };
    }

    private static RiskConfig GenerateRiskConfig(ParameterBounds bounds, Random rng)
    {
        var maxOpenTrades = NextFrom(bounds.MaxOpenTradesOptions, rng);
        var cooldownValue = NextFrom(bounds.CooldownCandlesOptions, rng);

        if (bounds.PositionSizeMode == PositionSizeMode.RiskBased)
        {
            var autoLeverage = bounds.IncludeAutoLeverage && rng.Next(2) == 0;
            var leverage = autoLeverage
                ? 1m
                : NextFromRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, rng);

            return new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = NextFrom(bounds.RiskPerTradePercentOptions, rng),
                AutoLeverage = autoLeverage,
                Leverage = leverage,
                MaxOpenTrades = maxOpenTrades,
                CooldownValue = cooldownValue,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            };
        }

        return new RiskConfig
        {
            PositionSizeType = PositionSizeType.PercentWallet,
            PositionSizeValue = NextFrom(bounds.PositionSizeOptions, rng),
            Leverage = NextFromRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, rng),
            MaxOpenTrades = maxOpenTrades,
            CooldownValue = cooldownValue,
            CooldownUnit = CooldownUnit.Candles,
            AllowSameCandleReentry = false,
        };
    }

    private static TrendFilterConfig? GenerateTrendFilter(ParameterBounds bounds, Random rng, Direction direction)
    {
        if (!bounds.IncludeTrendFilter || bounds.TrendFilterPairs.Length == 0 || rng.Next(2) == 0)
        {
            return null;
        }

        var filterType = NextFrom(bounds.TrendFilterTypes, rng);
        var filterOperator = NextFrom(bounds.TrendFilterOperators, rng);
        var pair = NextFrom(bounds.TrendFilterPairs, rng);

        if (pair.Length != 2 || pair[0] <= 0 || pair[1] <= 0 || pair[0] >= pair[1])
        {
            throw new InvalidOperationException("Trend filter pairs must contain exactly two ascending positive periods.");
        }

        // PriceAboveEma uses only the slow period as a single EMA
        if (filterType == TrendFilterType.PriceAboveEma)
        {
            return new TrendFilterConfig
            {
                Enabled = true,
                Type = filterType,
                Period = pair[1],
                FastPeriod = 0,
                SlowPeriod = 0,
                Operator = filterOperator,
                AppliesTo = direction,
            };
        }

        return new TrendFilterConfig
        {
            Enabled = true,
            Type = filterType,
            FastPeriod = pair[0],
            SlowPeriod = pair[1],
            Operator = filterOperator,
            AppliesTo = direction,
        };
    }

    private static string BuildDescription(
        IReadOnlyList<EntryConditionConfig> conditions,
        EntryLogic entryLogic,
        ExitConfig exit,
        RiskConfig risk,
        TrendFilterConfig? trendFilter,
        Direction direction)
    {
        var parts = conditions.Select(BuildConditionDescription).ToList();
        var separator = entryLogic == EntryLogic.All ? " + " : " | ";
        var description = string.Join(separator, parts);

        var sizeLabel = risk.PositionSizeType == PositionSizeType.RiskBased
            ? $"R:{Format(risk.RiskPerTradePercent)}%/trade"
            : $"Size:{Format(risk.PositionSizeValue)}%";

        var leverageLabel = risk.AutoLeverage ? "AutoLev" : $"Lev:{Format(risk.Leverage)}x";
        var stopLossLabel = exit.StopLoss.Type == ExitRuleType.AtrInitial
            ? $"SL:ATRx{Format(exit.StopLoss.AtrMultiplier)}"
            : $"SL:{Format(exit.StopLoss.Value)}%";

        description += $" | {entryLogic} | {stopLossLabel} TP:{Format(exit.TakeProfit.Value)}% {leverageLabel} {sizeLabel}";
        description += $" | {direction.ToString().ToUpperInvariant()}";

        if (exit.ExitOnOppositeSignal)
        {
            description += " | ExitOnOpp";
        }

        if (risk.MaxOpenTrades > 1)
        {
            description += $" | MaxTrades:{risk.MaxOpenTrades}";
        }

        if (trendFilter is not null && trendFilter.Enabled)
        {
            var trendDesc = trendFilter.Type switch
            {
                TrendFilterType.PriceAboveEma => $"Trend:PriceEMA({trendFilter.Period})",
                _ => $"Trend:EMA({trendFilter.FastPeriod},{trendFilter.SlowPeriod})"
            };
            description += $" | {trendDesc} {trendFilter.Operator}";
        }

        return description;
    }

    private static string BuildConditionDescription(EntryConditionConfig condition)
    {
        return condition.Params switch
        {
            RsiParams rsi => $"RSI({rsi.Period}) {rsi.Operator} {Format(rsi.Value)}",
            MacdParams macd => $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) {macd.Operator}",
            PriceVsEmaParams { Operator: "near" } priceVsEma => $"Price near EMA({priceVsEma.Period}) <= {Format(priceVsEma.DistanceValue)}%",
            PriceVsEmaParams priceVsEma => $"Price {priceVsEma.Operator} EMA({priceVsEma.Period})",
            _ => condition.Type.ToString(),
        };
    }

    private static T NextFrom<T>(IReadOnlyList<T> options, Random rng)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            throw new InvalidOperationException("Optimizer parameter options must not be empty.");
        }

        return options[rng.Next(options.Count)];
    }

    private static decimal NextFromRange(decimal min, decimal max, decimal step, Random rng)
    {
        if (step <= 0)
        {
            throw new InvalidOperationException("Optimizer range step must be greater than zero.");
        }

        if (max < min)
        {
            throw new InvalidOperationException("Optimizer range max must be greater than or equal to min.");
        }

        var values = new List<decimal>();
        for (var value = min; value <= max; value += step)
        {
            values.Add(decimal.Round(value, 8));
        }

        return NextFrom(values, rng);
    }

    private static void ValidateBounds(ParameterBounds bounds)
    {
        EnsureRange(bounds.StopLossMin, bounds.StopLossMax, bounds.StopLossStep, nameof(bounds.StopLossMin));
        EnsureRange(bounds.TakeProfitMin, bounds.TakeProfitMax, bounds.TakeProfitStep, nameof(bounds.TakeProfitMin));
        EnsureRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, nameof(bounds.LeverageMin));

        if (bounds.StopLossTypes.Length == 0)
        {
            throw new InvalidOperationException("Optimizer bounds must include at least one stop-loss type.");
        }

        if (bounds.StopLossTypes.Contains(ExitRuleType.AtrInitial))
        {
            if (bounds.AtrMultiplierOptions.Length == 0)
            {
                throw new InvalidOperationException("Optimizer bounds must include at least one ATR multiplier when using AtrInitial stop-loss type.");
            }

            if (bounds.AtrPeriodOptions.Length == 0)
            {
                throw new InvalidOperationException("Optimizer bounds must include at least one ATR period when using AtrInitial stop-loss type.");
            }
        }

        if (bounds.PositionSizeMode == PositionSizeMode.RiskBased)
        {
            if (bounds.RiskPerTradePercentOptions.Length == 0)
            {
                throw new InvalidOperationException("Optimizer bounds must include at least one RiskPerTradePercent option when using RiskBased sizing mode.");
            }
        }
        else if (bounds.PositionSizeOptions.Length == 0)
        {
            throw new InvalidOperationException("Optimizer bounds must include at least one option for each parameter family.");
        }

        if (bounds.Directions.Length == 0
            || bounds.RsiPeriods.Length == 0
            || bounds.RsiThresholds.Length == 0
            || bounds.RsiOperators.Length == 0
            || bounds.MacdFastPeriods.Length == 0
            || bounds.MacdSlowPeriods.Length == 0
            || bounds.MacdSignalPeriods.Length == 0
            || bounds.MacdOperators.Length == 0
            || bounds.EmaPeriods.Length == 0
            || bounds.EmaProximityPercents.Length == 0
            || bounds.PriceVsEmaOperators.Length == 0
            || bounds.ExitOnOppositeSignalOptions.Length == 0
            || bounds.MaxOpenTradesOptions.Length == 0
            || bounds.CooldownCandlesOptions.Length == 0)
        {
            throw new InvalidOperationException("Optimizer bounds must include at least one option for each parameter family.");
        }
    }

    private static void EnsureRange(decimal min, decimal max, decimal step, string parameterName)
    {
        if (max < min)
        {
            throw new InvalidOperationException($"{parameterName} range is invalid because max is less than min.");
        }

        if (step <= 0)
        {
            throw new InvalidOperationException($"{parameterName} range step must be greater than zero.");
        }
    }

    private static string Format(decimal? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
    }

    private sealed record SignalTemplate(IReadOnlyList<EntryConditionType> ConditionTypes, EntryLogic EntryLogic);
}