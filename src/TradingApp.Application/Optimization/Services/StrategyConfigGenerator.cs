using System.Globalization;
using TradingApp.Application.Optimization.Models;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.Optimization.Services;

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
            var conditions = GenerateEntryConditions(template, bounds, rng, sampleIndex);
            var exit = GenerateExitConfig(bounds, rng);
            var risk = GenerateRiskConfig(bounds, rng);
            var trendFilter = GenerateTrendFilter(bounds, rng);

            var config = new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Signal,
                StrategyName = $"Optimizer-{sampleIndex + 1}",
                Exchange = "Hyperliquid",
                Market = symbol,
                Timeframe = "15m",
                Direction = Direction.Long,
                Enabled = true,
                TemplateId = "custom_signal",
                EntryLogic = template.EntryLogic,
                EntryConditions = conditions,
                TrendFilter = trendFilter,
                Exit = exit,
                Risk = risk,
            };

            results.Add(new GeneratedStrategy(config, BuildDescription(conditions, template.EntryLogic, exit, risk, trendFilter)));
        }

        return results;
    }

    private static IReadOnlyList<EntryConditionConfig> GenerateEntryConditions(
        SignalTemplate template,
        ParameterBounds bounds,
        Random rng,
        int sampleIndex)
    {
        var conditions = new List<EntryConditionConfig>(template.ConditionTypes.Count);

        for (var conditionIndex = 0; conditionIndex < template.ConditionTypes.Count; conditionIndex++)
        {
            var conditionType = template.ConditionTypes[conditionIndex];
            var conditionId = $"opt-{sampleIndex + 1}-{conditionIndex + 1}-{conditionType.ToString().ToLowerInvariant()}";

            conditions.Add(conditionType switch
            {
                EntryConditionType.Rsi => new EntryConditionConfig
                {
                    Id = conditionId,
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI Oversold",
                    Params = new RsiParams
                    {
                        Period = NextFrom(bounds.RsiPeriods, rng),
                        Operator = "lt",
                        Value = NextFrom(bounds.RsiThresholds, rng),
                    },
                },
                EntryConditionType.Macd => new EntryConditionConfig
                {
                    Id = conditionId,
                    Enabled = true,
                    Type = EntryConditionType.Macd,
                    Label = "MACD Bullish Cross",
                    Params = CreateMacdParams(bounds, rng),
                },
                EntryConditionType.PriceVsEma => new EntryConditionConfig
                {
                    Id = conditionId,
                    Enabled = true,
                    Type = EntryConditionType.PriceVsEma,
                    Label = "Price Near EMA",
                    Params = new PriceVsEmaParams
                    {
                        Period = NextFrom(bounds.EmaPeriods, rng),
                        Operator = "near",
                        DistanceType = "percent",
                        DistanceValue = NextFrom(bounds.EmaProximityPercents, rng),
                    },
                },
                _ => throw new InvalidOperationException($"Unsupported optimizer entry condition type: {conditionType}.")
            });
        }

        return conditions;
    }

    private static MacdParams CreateMacdParams(ParameterBounds bounds, Random rng)
    {
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
                    Operator = "cross_above_signal",
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
        return new ExitConfig
        {
            TakeProfit = new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.FixedPercent,
                Value = NextFromRange(bounds.TakeProfitMin, bounds.TakeProfitMax, bounds.TakeProfitStep, rng),
            },
            StopLoss = new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.FixedPercent,
                Value = NextFromRange(bounds.StopLossMin, bounds.StopLossMax, bounds.StopLossStep, rng),
            },
            ExitOnOppositeSignal = false,
        };
    }

    private static RiskConfig GenerateRiskConfig(ParameterBounds bounds, Random rng)
    {
        return new RiskConfig
        {
            PositionSizeType = PositionSizeType.PercentWallet,
            PositionSizeValue = NextFrom(bounds.PositionSizeOptions, rng),
            Leverage = NextFromRange(bounds.LeverageMin, bounds.LeverageMax, bounds.LeverageStep, rng),
            MaxOpenTrades = 1,
            CooldownValue = 1,
            CooldownUnit = CooldownUnit.Candles,
            AllowSameCandleReentry = false,
        };
    }

    private static TrendFilterConfig? GenerateTrendFilter(ParameterBounds bounds, Random rng)
    {
        if (!bounds.IncludeTrendFilter || bounds.TrendFilterPairs.Length == 0 || rng.Next(2) == 0)
        {
            return null;
        }

        var pair = NextFrom(bounds.TrendFilterPairs, rng);
        if (pair.Length != 2 || pair[0] <= 0 || pair[1] <= 0 || pair[0] >= pair[1])
        {
            throw new InvalidOperationException("Trend filter pairs must contain exactly two ascending positive periods.");
        }

        return new TrendFilterConfig
        {
            Enabled = true,
            Type = TrendFilterType.EmaCross,
            FastPeriod = pair[0],
            SlowPeriod = pair[1],
            Operator = TrendOperator.Above,
            AppliesTo = Direction.Long,
        };
    }

    private static string BuildDescription(
        IReadOnlyList<EntryConditionConfig> conditions,
        EntryLogic entryLogic,
        ExitConfig exit,
        RiskConfig risk,
        TrendFilterConfig? trendFilter)
    {
        var parts = conditions.Select(BuildConditionDescription).ToList();
        var separator = entryLogic == EntryLogic.All ? " + " : " | ";
        var description = string.Join(separator, parts);

        description += $" | {entryLogic} | SL:{Format(exit.StopLoss.Value)}% TP:{Format(exit.TakeProfit.Value)}% Lev:{Format(risk.Leverage)}x Size:{Format(risk.PositionSizeValue)}%";

        if (trendFilter is not null && trendFilter.Enabled)
        {
            description += $" | Trend:EMA({trendFilter.FastPeriod},{trendFilter.SlowPeriod})";
        }

        return description;
    }

    private static string BuildConditionDescription(EntryConditionConfig condition)
    {
        return condition.Params switch
        {
            RsiParams rsi => $"RSI({rsi.Period}) {rsi.Operator} {Format(rsi.Value)}",
            MacdParams macd => $"MACD({macd.FastPeriod},{macd.SlowPeriod},{macd.SignalPeriod}) {macd.Operator}",
            PriceVsEmaParams priceVsEma => $"Price near EMA({priceVsEma.Period}) <= {Format(priceVsEma.DistanceValue)}%",
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

        if (bounds.PositionSizeOptions.Length == 0
            || bounds.RsiPeriods.Length == 0
            || bounds.RsiThresholds.Length == 0
            || bounds.MacdFastPeriods.Length == 0
            || bounds.MacdSlowPeriods.Length == 0
            || bounds.MacdSignalPeriods.Length == 0
            || bounds.EmaPeriods.Length == 0
            || bounds.EmaProximityPercents.Length == 0)
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