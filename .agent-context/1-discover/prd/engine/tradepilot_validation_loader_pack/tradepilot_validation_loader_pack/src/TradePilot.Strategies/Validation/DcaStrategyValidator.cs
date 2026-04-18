using FluentValidation;
using TradePilot.Strategies.Parsing;

namespace TradePilot.Strategies.Validation;

public sealed class DcaStrategyValidator : StrategyYamlBaseValidator<DcaStrategyYaml>
{
    public DcaStrategyValidator()
    {
        RuleFor(x => x.StrategyType).Equal("dca");
        RuleFor(x => x.Direction)
            .Must(direction => direction is "long" or "short")
            .WithMessage("DCA strategies must have direction 'long' or 'short'.");

        RuleFor(x => x.Trigger).NotNull();
        RuleFor(x => x.Ladder).NotNull();

        When(x => x.Trigger is not null, () =>
        {
            RuleFor(x => x.Trigger!.Activation).NotNull().Must(x => x!.Count > 0)
                .WithMessage("trigger.activation must contain at least one condition.");
            RuleForEach(x => x.Trigger!.Activation!).SetValidator(new ConditionYamlValidator()!)
                .When(x => x.Trigger!.Activation is not null);
        });

        When(x => x.Ladder is not null, () =>
        {
            RuleFor(x => x.Ladder!.OrderSizingMode).NotEmpty();
            RuleFor(x => x.Ladder!.BaseOrderSize).NotNull();
            RuleFor(x => x.Ladder!.Scaling).NotNull();
            RuleFor(x => x.Ladder!.Placement).NotNull();

            RuleFor(x => x.Ladder!.BaseOrderSize!.Value).GreaterThan(0);
            RuleFor(x => x.Ladder!.BaseOrderSize!.Currency).NotEmpty();

            RuleFor(x => x.Ladder!.Scaling!.Mode).NotEmpty();
            RuleFor(x => x.Ladder!.Scaling!.MaxOrders).GreaterThan(0);
            RuleFor(x => x.Ladder!.Scaling!.StepPercent)
                .GreaterThan(0)
                .When(x => x.Ladder!.Scaling!.Mode == "percent_steps");

            RuleFor(x => x.Ladder!.Placement!.ReferencePrice).NotEmpty();
            RuleFor(x => x.Ladder!.Placement!.PlaceOrders).NotNull().Must(x => x.Count > 0)
                .WithMessage("ladder.placement.place_orders must contain at least one placement rule.");
        });

        When(x => x.Budget?.MaxTotalInvestment is not null, () =>
        {
            RuleFor(x => x.Budget!.MaxTotalInvestment!.Value).GreaterThan(0);
            RuleFor(x => x.Budget!.MaxTotalInvestment!.Currency).NotEmpty();
        });
    }
}