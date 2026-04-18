using FluentValidation;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Validation;

public sealed class SignalStrategyValidator : StrategyYamlBaseValidator<SignalStrategyYaml>
{
    public SignalStrategyValidator()
    {
        RuleFor(x => x.StrategyType).Equal("signal");
        RuleFor(x => x.Direction)
            .Must(direction => direction is "long" or "short")
            .WithMessage("Signal strategies must have direction 'long' or 'short'.");

        RuleFor(x => x.Core).NotNull();
        RuleFor(x => x.Filters).NotNull();

        When(x => x.Core is not null, () =>
        {
            RuleForEach(x => x.Core!.MarketBias!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Core!.MarketBias is not null);
            RuleForEach(x => x.Core!.Setup!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Core!.Setup is not null);
            RuleForEach(x => x.Core!.EntryTrigger!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Core!.EntryTrigger is not null);

            RuleFor(x => x.Core!.MarketBias).NotNull().Must(x => x!.Count > 0)
                .WithMessage("core.market_bias must contain at least one condition.");
            RuleFor(x => x.Core!.Setup).NotNull().Must(x => x!.Count > 0)
                .WithMessage("core.setup must contain at least one condition.");
            RuleFor(x => x.Core!.EntryTrigger).NotNull().Must(x => x!.Count > 0)
                .WithMessage("core.entry_trigger must contain at least one condition.");
        });

        When(x => x.Filters is not null, () =>
        {
            RuleForEach(x => x.Filters!.Hard!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Filters!.Hard is not null);
            RuleForEach(x => x.Filters!.Soft!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Filters!.Soft is not null);
        });
    }
}