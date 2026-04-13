using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class DrawdownEvaluatorTests
{
    private static readonly IReadOnlyList<DrawdownTier> DefaultTiers =
    [
        new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.75m },
        new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.50m },
        new DrawdownTier { ThresholdPercent = 15m, ScalingFactor = 0.0m },
    ];

    [TestMethod]
    public void GivenEquityAboveHwm_WhenEvaluated_ThenHwmRatchetsUp()
    {
        var result = DrawdownEvaluator.Evaluate(10_500m, 10_000m, DefaultTiers);

        result.NewHighWaterMark.Should().Be(10_500m);
        result.DrawdownPercent.Should().Be(0m);
        result.ScalingFactor.Should().Be(1.0m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt7Percent_WhenEvaluated_ThenScalingIs075()
    {
        var result = DrawdownEvaluator.Evaluate(9_300m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(7m);
        result.ScalingFactor.Should().Be(0.75m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt12Percent_WhenEvaluated_ThenScalingIs050()
    {
        var result = DrawdownEvaluator.Evaluate(8_800m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(12m);
        result.ScalingFactor.Should().Be(0.50m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenDrawdownAt16Percent_WhenEvaluated_ThenIsHalted()
    {
        var result = DrawdownEvaluator.Evaluate(8_400m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(16m);
        result.ScalingFactor.Should().Be(0.0m);
        result.IsHalted.Should().BeTrue();
    }

    [TestMethod]
    public void GivenDrawdownExactlyAtTierThreshold_WhenEvaluated_ThenTierApplies()
    {
        var result = DrawdownEvaluator.Evaluate(9_500m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(5m);
        result.ScalingFactor.Should().Be(0.75m);
    }

    [TestMethod]
    public void GivenDrawdownBelowFirstTier_WhenEvaluated_ThenFullScaling()
    {
        var result = DrawdownEvaluator.Evaluate(9_700m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(3m);
        result.ScalingFactor.Should().Be(1.0m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenNoTiers_WhenEvaluated_ThenFullScaling()
    {
        var result = DrawdownEvaluator.Evaluate(8_000m, 10_000m, []);

        result.ScalingFactor.Should().Be(1.0m);
        result.IsHalted.Should().BeFalse();
    }

    [TestMethod]
    public void GivenEquityDecline_WhenEvaluated_ThenHwmDoesNotDecrease()
    {
        var result = DrawdownEvaluator.Evaluate(9_000m, 10_000m, DefaultTiers);

        result.NewHighWaterMark.Should().Be(10_000m);
    }

    [TestMethod]
    public void GivenRecoveryFromHaltToBelow15Percent_WhenEvaluated_ThenNotHalted()
    {
        var result = DrawdownEvaluator.Evaluate(8_600m, 10_000m, DefaultTiers);

        result.DrawdownPercent.Should().Be(14m);
        result.ScalingFactor.Should().Be(0.50m);
        result.IsHalted.Should().BeFalse();
    }
}