using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class StopLossDistanceResolverTests
{
    [TestMethod]
    public void GivenFixedPercentStopLoss_WhenResolved_ThenReturnsConfigValue()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.FixedPercent,
            Value = 2m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 100m);

        result.Should().Be(2m);
    }

    [TestMethod]
    public void GivenAtrTrailingStopLoss_WhenResolved_ThenReturnsComputedPercent()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrTrailing,
            AtrMultiplier = 3m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 100m, anchorPrice: 10_000m);

        result.Should().Be(3m);
    }

    [TestMethod]
    public void GivenAtrTrailingStopLossWithDefaultMultiplier_WhenResolved_ThenUsesDefaultOfThree()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrTrailing,
            AtrMultiplier = null,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 50m, anchorPrice: 5_000m);

        result.Should().Be(3m);
    }

    [TestMethod]
    public void GivenAtrTrailingStopLossWithZeroAtr_WhenResolved_ThenReturnsNull()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrTrailing,
            AtrMultiplier = 3m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 0m, anchorPrice: 10_000m);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenAtrTrailingStopLossWithZeroPrice_WhenResolved_ThenReturnsNull()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrTrailing,
            AtrMultiplier = 3m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 100m, anchorPrice: 0m);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenDisabledStopLossWithGridBreakdownThreshold_WhenResolved_ThenReturnsBreakdownThreshold()
    {
        var config = new ExitRuleConfig
        {
            Enabled = false,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 100m, gridBreakdownThreshold: 5m);

        result.Should().Be(5m);
    }

    [TestMethod]
    public void GivenDisabledStopLossWithNoFallback_WhenResolved_ThenReturnsNull()
    {
        var config = new ExitRuleConfig
        {
            Enabled = false,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 100m);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenSwingLowStopLoss_WhenResolved_ThenReturnsNull()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.SwingLow,
            Lookback = 10,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 100m);

        result.Should().BeNull();
    }
}