using FluentValidation;
using System.Linq;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Validation;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeKnownStrategyType<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => value is "signal" or "dca" or "grid")
            .WithMessage("strategy_type must be one of: signal, dca, grid.");

    public static IRuleBuilderOptions<T, string> MustBeKnownDirection<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => value is "long" or "short" or "neutral")
            .WithMessage("direction must be one of: long, short, neutral.");

    public static IRuleBuilderOptions<T, string> MustBeKnownValueRefType<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => value is "static" or "indicator" or "signal" or "field")
            .WithMessage("rhs.type must be one of: static, indicator, signal, field.");

    public static IRuleBuilderOptions<T, string> MustBeKnownConditionOperator<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => new[]
        {
            ">", ">=", "<", "<=", "==", "!=", "crosses_above", "crosses_below", "in", "not_in"
        }.Contains(value))
        .WithMessage("operator is not supported.");

    public static IRuleBuilderOptions<T, string> MustBeKnownEntryType<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => value is "market" or "limit" or "market_on_close" or "limit_on_retest")
            .WithMessage("execution.entry_type is not supported.");

    public static IRuleBuilderOptions<T, string> MustBeKnownSession<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value => value is "london" or "new_york" or "asia" or "custom")
            .WithMessage("Unknown session name.");
}