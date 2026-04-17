using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class PositionSizeResolverTests
{
    [TestMethod]
    public void GivenPercentWallet_WhenResolved_ThenReturnsPercentageOfEquity()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.PercentWallet,
            PositionSizeValue = 5m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m);

        result.Should().Be(500m);
    }

    [TestMethod]
    public void GivenPercentWalletWithZeroEquity_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.PercentWallet,
            PositionSizeValue = 5m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 0m);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenFixedNotional_WhenResolved_ThenReturnsFixedValue()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 1_000m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 50_000m);

        result.Should().Be(1_000m);
    }

    [TestMethod]
    public void GivenRiskBased1PctRiskAnd2PctSL_WhenResolved_ThenReturns5000()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 2.0m);

        result.Should().Be(5_000m);
    }

    [TestMethod]
    public void GivenRiskBased1PctRiskAnd5PctSL_WhenResolved_ThenReturns2000()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 5.0m);

        result.Should().Be(2_000m);
    }

    [TestMethod]
    public void GivenRiskBasedAfterLoss_WhenResolved_ThenNotionalShrinks()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 9_900m, stopLossPercent: 2.0m);

        result.Should().Be(4_950m);
    }

    [TestMethod]
    public void GivenRiskBasedWithSequentialLosses_WhenResolved_ThenNotionalDecreases()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var notional1 = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 2.0m);
        var notional2 = PositionSizeResolver.ResolveNotional(risk, accountEquity: 9_900m, stopLossPercent: 2.0m);
        var notional3 = PositionSizeResolver.ResolveNotional(risk, accountEquity: 9_801m, stopLossPercent: 2.0m);

        notional1.Should().Be(5_000m);
        notional2.Should().Be(4_950m);
        notional3.Should().Be(4_900.5m);
        notional2.Should().BeLessThan(notional1);
        notional3.Should().BeLessThan(notional2);
    }

    [TestMethod]
    public void GivenRiskBasedWithNullStopLoss_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: null);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenRiskBasedWithZeroStopLoss_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 0m);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenRiskBasedWithNullRiskPercent_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = null,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 2.0m);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenRiskBasedWithZeroEquity_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 0m, stopLossPercent: 2.0m);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenRiskBasedSizing_WhenResolveInitialR_ThenReturnsDollarRisk()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1m,
        };

        var result = PositionSizeResolver.ResolveInitialR(risk, accountEquity: 10_000m);

        result.Should().Be(100m);
    }

    [TestMethod]
    public void GivenNonRiskBasedSizing_WhenResolveInitialR_ThenReturnsNull()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 1_000m,
        };

        var result = PositionSizeResolver.ResolveInitialR(risk, accountEquity: 10_000m);

        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenRiskBasedWithNegativeEquity_WhenResolved_ThenReturnsZero()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.RiskBased,
            RiskPerTradePercent = 1.0m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: -500m, stopLossPercent: 2.0m);

        result.Should().Be(0m);
    }

    [TestMethod]
    public void GivenPercentWalletWithStopLossParam_WhenResolved_ThenStopLossIsIgnored()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.PercentWallet,
            PositionSizeValue = 5m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 2.0m);

        result.Should().Be(500m);
    }

    [TestMethod]
    public void GivenFixedNotionalWithStopLossParam_WhenResolved_ThenStopLossIsIgnored()
    {
        var risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 1_000m,
        };

        var result = PositionSizeResolver.ResolveNotional(risk, accountEquity: 10_000m, stopLossPercent: 2.0m);

        result.Should().Be(1_000m);
    }
}