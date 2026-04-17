using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

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
    public void GivenAtrInitialStopLoss_WhenResolved_ThenReturnsComputedPercent()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
            AtrMultiplier = 2m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 500m, anchorPrice: 50_000m);

        result.Should().Be(2m);
    }

    [TestMethod]
    public void GivenAtrInitialStopLossWithDefaultMultiplier_WhenResolved_ThenUsesDefaultOfTwo()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 500m, anchorPrice: 50_000m);

        result.Should().Be(2m);
    }

    [TestMethod]
    public void GivenAtrInitialStopLossWithNoAtrAndFallbackValue_WhenResolved_ThenReturnsFallbackPercent()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
            AtrMultiplier = 2m,
            Value = 3m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: 0m, anchorPrice: 50_000m);

        result.Should().Be(3m);
    }

    [TestMethod]
    public void GivenAtrInitialStopLossWithNoAtrAndNoFallback_WhenResolved_ThenReturnsNull()
    {
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
            AtrMultiplier = 2m,
        };

        var result = StopLossDistanceResolver.Resolve(config, atr: null, anchorPrice: 50_000m);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenRiskBasedSizingWithAtrInitialStopLoss_WhenAtrDoubles_ThenResolvedNotionalHalves()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1m,
        };
        var config = new ExitRuleConfig
        {
            Enabled = true,
            Type = ExitRuleType.AtrInitial,
            AtrMultiplier = 2m,
        };

        var lowVolStopPercent = StopLossDistanceResolver.Resolve(config, atr: 500m, anchorPrice: 50_000m);
        var highVolStopPercent = StopLossDistanceResolver.Resolve(config, atr: 1_000m, anchorPrice: 50_000m);
        var lowVolNotional = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, lowVolStopPercent);
        var highVolNotional = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, highVolStopPercent);

        lowVolStopPercent.Should().Be(2m);
        highVolStopPercent.Should().Be(4m);
        lowVolNotional.Should().Be(5_000m);
        highVolNotional.Should().Be(2_500m);
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