using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Validation;

public sealed class MarketYamlValidator : AbstractValidator<MarketYaml>
{
    public MarketYamlValidator()
    {
        RuleFor(x => x.Symbol).NotEmpty();
        RuleFor(x => x.Venue).NotEmpty();
    }
}

public sealed class IndicatorYamlValidator : AbstractValidator<IndicatorYaml>
{
    public IndicatorYamlValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty();
        RuleFor(x => x.Timeframe).NotEmpty();
        RuleFor(x => x.Params).NotNull();
    }
}

public sealed class SignalYamlValidator : AbstractValidator<SignalYaml>
{
    public SignalYamlValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty();
        RuleFor(x => x.Source).NotNull();
    }
}

public sealed class ValueRefYamlValidator : AbstractValidator<ValueRefYaml>
{
    public ValueRefYamlValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MustBeKnownValueRefType();

        When(x => x.Type == "static", () =>
        {
            RuleFor(x => x.Value).NotNull()
                .WithMessage("rhs.value is required when rhs.type = static.");
        });

        When(x => x.Type == "indicator" || x.Type == "signal", () =>
        {
            RuleFor(x => x.Id).NotEmpty()
                .WithMessage("rhs.id is required when rhs.type = indicator or signal.");
        });

        When(x => x.Type == "field", () =>
        {
            RuleFor(x => x.Path).NotEmpty()
                .WithMessage("rhs.path is required when rhs.type = field.");
        });
    }
}

public sealed class ConditionYamlValidator : AbstractValidator<ConditionYaml>
{
    public ConditionYamlValidator()
    {
        RuleFor(x => x.Lhs).NotEmpty();
        RuleFor(x => x.Operator).NotEmpty().MustBeKnownConditionOperator();
        RuleFor(x => x.Rhs).NotNull().SetValidator(new ValueRefYamlValidator()!);
        RuleFor(x => x.LookbackBars).GreaterThan(0).When(x => x.LookbackBars.HasValue);
    }
}

public sealed class ExecutionYamlValidator : AbstractValidator<ExecutionYaml>
{
    public ExecutionYamlValidator()
    {
        RuleFor(x => x.EntryType).NotEmpty().MustBeKnownEntryType();
        RuleFor(x => x.MaxReentries).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.AllowedSessions!).MustBeKnownSession().When(x => x.AllowedSessions is not null);
        RuleFor(x => x.MaxSpreadBps).GreaterThanOrEqualTo(0).When(x => x.MaxSpreadBps.HasValue);
        RuleFor(x => x.MaxSlippageBps).GreaterThanOrEqualTo(0).When(x => x.MaxSlippageBps.HasValue);
    }
}

public sealed class PositionSizingYamlValidator : AbstractValidator<PositionSizingYaml>
{
    public PositionSizingYamlValidator()
    {
        RuleFor(x => x.Mode).NotEmpty();

        When(x => x.Mode == "fixed_quote" || x.Mode == "fixed_base", () =>
        {
            RuleFor(x => x.Value).NotNull().GreaterThan(0);
        });

        When(x => x.Mode == "percent_equity", () =>
        {
            RuleFor(x => x.RiskPercent).NotNull().GreaterThan(0);
        });
    }
}

public sealed class StopLossYamlValidator : AbstractValidator<StopLossYaml>
{
    public StopLossYamlValidator()
    {
        RuleFor(x => x.Mode).NotEmpty();
    }
}

public sealed class TakeProfitYamlValidator : AbstractValidator<TakeProfitYaml>
{
    public TakeProfitYamlValidator()
    {
        RuleFor(x => x.Mode).NotEmpty();

        When(x => x.Mode == "rr_based", () =>
        {
            RuleFor(x => x.RrTarget).NotNull().GreaterThan(0);
        });

        When(x => x.Mode == "fixed_percent" || x.Mode == "blended_position", () =>
        {
            RuleFor(x => x.TargetPercent).NotNull().GreaterThan(0);
        });
    }
}

public sealed class ExposureCapYamlValidator : AbstractValidator<ExposureCapYaml>
{
    public ExposureCapYamlValidator()
    {
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
    }
}

public sealed class RiskYamlValidator : AbstractValidator<RiskYaml>
{
    public RiskYamlValidator()
    {
        RuleFor(x => x.PositionSizing).NotNull().SetValidator(new PositionSizingYamlValidator()!);
        RuleFor(x => x.StopLoss).NotNull().SetValidator(new StopLossYamlValidator()!);
        RuleFor(x => x.TakeProfit).NotNull().SetValidator(new TakeProfitYamlValidator()!);
        RuleFor(x => x.MaxTotalExposure).SetValidator(new ExposureCapYamlValidator()!).When(x => x.MaxTotalExposure is not null);
    }
}

public sealed class TelemetryYamlValidator : AbstractValidator<TelemetryYaml>
{
    public TelemetryYamlValidator()
    {
        // Presence is enough for starter pack; booleans are value types.
    }
}

public abstract class StrategyYamlBaseValidator<TStrategy> : AbstractValidator<TStrategy>
    where TStrategy : StrategyYamlBase
{
    protected StrategyYamlBaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.StrategyType).NotEmpty().MustBeKnownStrategyType();
        RuleFor(x => x.Direction).NotEmpty().MustBeKnownDirection();
        RuleFor(x => x.Market).NotNull().SetValidator(new MarketYamlValidator()!);
        RuleFor(x => x.Execution).NotNull().SetValidator(new ExecutionYamlValidator()!);
        RuleFor(x => x.Risk).NotNull().SetValidator(new RiskYamlValidator()!);
        RuleFor(x => x.Telemetry).NotNull().SetValidator(new TelemetryYamlValidator()!);

        RuleForEach(x => x.Indicators!).SetValidator(new IndicatorYamlValidator()!).When(x => x.Indicators is not null);
        RuleForEach(x => x.Signals!).SetValidator(new SignalYamlValidator()!).When(x => x.Signals is not null);
        RuleForEach(x => x.EnablementConditions!).SetValidator(new ConditionYamlValidator()!).When(x => x.EnablementConditions is not null);
        RuleForEach(x => x.DisablementConditions!).SetValidator(new ConditionYamlValidator()!).When(x => x.DisablementConditions is not null);

        RuleFor(x => x)
            .Custom((strategy, context) =>
            {
                var indicatorIds = strategy.Indicators?.Select(i => i.Id).ToList() ?? new List<string>();
                var signalIds = strategy.Signals?.Select(s => s.Id).ToList() ?? new List<string>();

                AddDuplicates(indicatorIds, "indicator", context);
                AddDuplicates(signalIds, "signal", context);

                var referencedIndicatorIds = ExtractReferencedIds(strategy, "indicator");
                foreach (var id in referencedIndicatorIds.Where(id => !indicatorIds.Contains(id)))
                {
                    context.AddFailure($"Referenced indicator id '{id}' does not exist on strategy '{strategy.Id}'.");
                }

                var referencedSignalIds = ExtractReferencedIds(strategy, "signal");
                foreach (var id in referencedSignalIds.Where(id => !signalIds.Contains(id)))
                {
                    context.AddFailure($"Referenced signal id '{id}' does not exist on strategy '{strategy.Id}'.");
                }
            });
    }

    private static void AddDuplicates(IEnumerable<string> ids, string category, ValidationContext<TStrategy> context)
    {
        var duplicates = ids
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            context.AddFailure($"Duplicate {category} id '{duplicate}'.");
        }
    }

    private static IEnumerable<string> ExtractReferencedIds(StrategyYamlBase strategy, string valueRefType)
    {
        static IEnumerable<ConditionYaml> Flatten(StrategyYamlBase s)
        {
            foreach (var c in s.EnablementConditions ?? Enumerable.Empty<ConditionYaml>()) yield return c;
            foreach (var c in s.DisablementConditions ?? Enumerable.Empty<ConditionYaml>()) yield return c;

            if (s is SignalStrategyYaml signal)
            {
                foreach (var c in signal.Core?.MarketBias ?? Enumerable.Empty<ConditionYaml>()) yield return c;
                foreach (var c in signal.Core?.Setup ?? Enumerable.Empty<ConditionYaml>()) yield return c;
                foreach (var c in signal.Core?.EntryTrigger ?? Enumerable.Empty<ConditionYaml>()) yield return c;
                foreach (var c in signal.Filters?.Hard ?? Enumerable.Empty<ConditionYaml>()) yield return c;
                foreach (var c in signal.Filters?.Soft ?? Enumerable.Empty<ConditionYaml>()) yield return c;
            }

            if (s is DcaStrategyYaml dca)
            {
                foreach (var c in dca.Trigger?.Activation ?? Enumerable.Empty<ConditionYaml>()) yield return c;
            }

            if (s is GridStrategyYaml grid)
            {
                foreach (var c in grid.Activation ?? Enumerable.Empty<ConditionYaml>()) yield return c;
            }
        }

        return Flatten(strategy)
            .Where(c => c.Rhs?.Type == valueRefType && !string.IsNullOrWhiteSpace(c.Rhs.Id))
            .Select(c => c.Rhs!.Id!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}