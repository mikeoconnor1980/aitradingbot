using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

public sealed class DcaStrategyEngine : IStrategyEngine
{
    public Task<StrategyEvaluationResult> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (config.Dca is null
            || config.Dca.BaseAmountUsd <= 0m
            || config.Dca.Allocations.Count == 0)
        {
            return Task.FromResult(Fail(
                [],
                "dca.configuration",
                "DCA configuration",
                "DCA configuration is incomplete.",
                $"baseAmount={config.Dca?.BaseAmountUsd ?? 0m}, allocations={config.Dca?.Allocations.Count ?? 0}",
                "base amount > 0 and at least one allocation"));
        }

        var rules = new List<RuleEvaluationResult>
        {
            Pass("dca.configuration", "DCA configuration", "DCA configuration is complete."),
        };

        if (config.Direction != Direction.Long)
        {
            return Task.FromResult(Fail(
                rules,
                "dca.direction",
                "DCA direction",
                "DCA currently supports long accumulation only.",
                config.Direction.ToString(),
                Direction.Long.ToString()));
        }

        rules.Add(Pass("dca.direction", "DCA direction", "DCA direction is supported."));

        var gates = config.Dca.GateConditions;
        if (gates is null)
        {
            return Task.FromResult(Pass(rules, "DCA buy window open."));
        }

        if (gates.MaxPriceUsd is > 0m && context.CurrentCandle.Close > gates.MaxPriceUsd.Value)
        {
            var reason = $"Price gate blocked DCA buy. Current price {context.CurrentCandle.Close} is above {gates.MaxPriceUsd.Value}.";
            return Task.FromResult(Fail(
                rules,
                "entry.price.maximum",
                "Maximum entry price",
                reason,
                context.CurrentCandle.Close.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"<= {gates.MaxPriceUsd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                context.CurrentCandle.Close,
                gates.MaxPriceUsd.Value));
        }

        if (gates.MaxPriceUsd is > 0m)
        {
            rules.Add(new RuleEvaluationResult(
                "entry.price.maximum",
                "Maximum entry price",
                TradePilot.Domain.Enums.RuleCategory.Entry,
                true,
                "Current price is within the configured DCA maximum.",
                true,
                ActualValue: context.CurrentCandle.Close.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActualNumericValue: context.CurrentCandle.Close,
                ExpectedValue: $"<= {gates.MaxPriceUsd.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                ExpectedNumericValue: gates.MaxPriceUsd.Value));
        }

        if (gates.MinFearGreedIndex.HasValue || gates.MaxFearGreedIndex.HasValue)
        {
            if (context.FearGreed is null)
            {
                return Task.FromResult(Fail(
                    rules,
                    "sentiment.fear_greed.available",
                    "Fear & Greed availability",
                    "Fear & Greed gate is enabled but no Fear & Greed reading is available.",
                    "unavailable",
                    "available"));
            }

            rules.Add(Pass("sentiment.fear_greed.available", "Fear & Greed availability", "Fear & Greed reading is available."));

            if (gates.MinFearGreedIndex.HasValue && context.FearGreed.Value < gates.MinFearGreedIndex.Value)
            {
                var reason = $"Fear & Greed gate blocked DCA buy. Current value {context.FearGreed.Value} is below {gates.MinFearGreedIndex.Value}.";
                return Task.FromResult(Fail(
                    rules,
                    "sentiment.fear_greed.minimum",
                    "Minimum Fear & Greed",
                    reason,
                    context.FearGreed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $">= {gates.MinFearGreedIndex.Value}",
                    context.FearGreed.Value,
                    gates.MinFearGreedIndex.Value));
            }

            if (gates.MinFearGreedIndex.HasValue)
            {
                rules.Add(NumericPass(
                    "sentiment.fear_greed.minimum",
                    "Minimum Fear & Greed",
                    context.FearGreed.Value,
                    $">= {gates.MinFearGreedIndex.Value}",
                    gates.MinFearGreedIndex.Value));
            }

            if (gates.MaxFearGreedIndex.HasValue && context.FearGreed.Value > gates.MaxFearGreedIndex.Value)
            {
                var reason = $"Fear & Greed gate blocked DCA buy. Current value {context.FearGreed.Value} is above {gates.MaxFearGreedIndex.Value}.";
                return Task.FromResult(Fail(
                    rules,
                    "sentiment.fear_greed.maximum",
                    "Maximum Fear & Greed",
                    reason,
                    context.FearGreed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"<= {gates.MaxFearGreedIndex.Value}",
                    context.FearGreed.Value,
                    gates.MaxFearGreedIndex.Value));
            }

            if (gates.MaxFearGreedIndex.HasValue)
            {
                rules.Add(NumericPass(
                    "sentiment.fear_greed.maximum",
                    "Maximum Fear & Greed",
                    context.FearGreed.Value,
                    $"<= {gates.MaxFearGreedIndex.Value}",
                    gates.MaxFearGreedIndex.Value));
            }
        }

        return Task.FromResult(Pass(rules, "DCA buy window open."));
    }

    private static StrategyEvaluationResult Fail(
        List<RuleEvaluationResult> priorRules,
        string ruleId,
        string name,
        string reason,
        string? actualValue,
        string? expectedValue,
        decimal? actualNumericValue = null,
        decimal? expectedNumericValue = null)
    {
        var rules = new List<RuleEvaluationResult>(priorRules)
        {
            new(
                ruleId,
                name,
                TradePilot.Domain.Enums.RuleCategory.Entry,
                false,
                reason,
                true,
                ActualValue: actualValue,
                ActualNumericValue: actualNumericValue,
                ExpectedValue: expectedValue,
                ExpectedNumericValue: expectedNumericValue),
        };

        return new StrategyEvaluationResult
        {
            SetupDetected = false,
            Reason = reason,
            Rules = rules,
            EvaluationShortCircuited = true,
        };
    }

    private static StrategyEvaluationResult Pass(List<RuleEvaluationResult> rules, string reason)
    {
        return new StrategyEvaluationResult
        {
            SetupDetected = true,
            Reason = reason,
            Rules = rules,
        };
    }

    private static RuleEvaluationResult Pass(string ruleId, string name, string reason)
    {
        return new RuleEvaluationResult(
            ruleId,
            name,
            TradePilot.Domain.Enums.RuleCategory.Entry,
            true,
            reason,
            true);
    }

    private static RuleEvaluationResult NumericPass(
        string ruleId,
        string name,
        decimal actual,
        string expected,
        decimal expectedNumeric)
    {
        return new RuleEvaluationResult(
            ruleId,
            name,
            TradePilot.Domain.Enums.RuleCategory.Entry,
            true,
            $"{name} passed.",
            true,
            ActualValue: actual.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActualNumericValue: actual,
            ExpectedValue: expected,
            ExpectedNumericValue: expectedNumeric);
    }
}
