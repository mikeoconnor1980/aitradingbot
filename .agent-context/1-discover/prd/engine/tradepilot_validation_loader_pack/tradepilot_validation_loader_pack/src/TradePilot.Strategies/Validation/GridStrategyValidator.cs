using FluentValidation;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Validation;

public sealed class GridStrategyValidator : StrategyYamlBaseValidator<GridStrategyYaml>
{
    public GridStrategyValidator()
    {
        RuleFor(x => x.StrategyType).Equal("grid");
        RuleFor(x => x.Direction)
            .Equal("neutral")
            .WithMessage("Grid strategies should default to direction 'neutral'.");

        RuleFor(x => x.Activation).NotNull().Must(x => x!.Count > 0)
            .WithMessage("activation must contain at least one condition.");
        RuleForEach(x => x.Activation!).SetValidator(new ConditionYamlValidator()!)
            .When(x => x.Activation is not null);

        RuleFor(x => x.Grid).NotNull();
        RuleFor(x => x.Inventory).NotNull();
        RuleFor(x => x.ProfitModel).NotNull();

        When(x => x.Grid is not null, () =>
        {
            RuleFor(x => x.Grid!.LowerBound).GreaterThan(0);
            RuleFor(x => x.Grid!.UpperBound).GreaterThan(x => x.Grid!.LowerBound);
            RuleFor(x => x.Grid!.GridCount).GreaterThan(1);
            RuleFor(x => x.Grid!.SpacingMode)
                .Must(mode => mode is "arithmetic" or "geometric")
                .WithMessage("grid.spacing_mode must be arithmetic or geometric.");
            RuleFor(x => x.Grid!.OrderSizeMode).NotEmpty();
            RuleFor(x => x.Grid!.OrderSize).NotNull();
            RuleFor(x => x.Grid!.OrderSize!.Value).GreaterThan(0);
            RuleFor(x => x.Grid!.OrderSize!.Currency).NotEmpty();
        });

        When(x => x.Rebalance is not null && x.Rebalance!.AutoRecenter == true, () =>
        {
            RuleFor(x => x.Rebalance!.RecenterThresholdPercent).NotNull().GreaterThan(0);
            RuleFor(x => x.Rebalance!.RecenterAction).NotEmpty();
        });
    }
}