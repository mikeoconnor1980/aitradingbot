using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Services;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class PortfolioHeatCalculatorTests
{
    [TestMethod]
    public void GivenPositionWithStopLoss_WhenEstimateRisk_ThenCalculatesFromSlDistance()
    {
        var position = new PositionDto
        {
            Asset = "BTC",
            Size = 0.1m,
            EntryPrice = 50_000m,
            StopLossPrice = 48_500m,
            MarginUsed = 500m
        };

        var risk = PortfolioHeatCalculator.EstimatePositionRisk(position);

        risk.Should().Be(150m);
    }

    [TestMethod]
    public void GivenPositionWithoutStopLoss_WhenEstimateRisk_ThenUsesMarginAsFallback()
    {
        var position = new PositionDto
        {
            Asset = "ETH",
            Size = 1m,
            EntryPrice = 3_000m,
            MarginUsed = 300m
        };

        var risk = PortfolioHeatCalculator.EstimatePositionRisk(position);

        risk.Should().Be(300m);
    }

    [TestMethod]
    public void GivenPositionWithZeroStopLoss_WhenEstimateRisk_ThenUsesMarginAsFallback()
    {
        var position = new PositionDto
        {
            Asset = "SOL",
            Size = 5m,
            EntryPrice = 150m,
            StopLossPrice = 0m,
            MarginUsed = 125m
        };

        var risk = PortfolioHeatCalculator.EstimatePositionRisk(position);

        risk.Should().Be(125m);
    }

    [TestMethod]
    public void GivenEmptyPositions_WhenCalculateFromPositions_ThenReturnsEmptyResult()
    {
        var result = PortfolioHeatCalculator.CalculateFromPositions([], 10_000m, 6m);

        result.HeatUsd.Should().Be(0m);
        result.HeatPercent.Should().Be(0m);
        result.Equity.Should().Be(10_000m);
        result.Entries.Should().BeEmpty();
        result.IsLimitEnabled.Should().BeTrue();
        result.IsLimitExceeded.Should().BeFalse();
    }

    [TestMethod]
    public void GivenZeroEquity_WhenCalculateFromPositions_ThenReturnsEmptyResult()
    {
        var positions = new[]
        {
            new PositionDto
            {
                Asset = "BTC",
                Size = 0.1m,
                EntryPrice = 50_000m,
                StopLossPrice = 49_000m,
                MarginUsed = 500m
            }
        };

        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 0m, 6m);

        result.HeatUsd.Should().Be(0m);
        result.HeatPercent.Should().Be(0m);
        result.Equity.Should().Be(0m);
        result.Entries.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenSinglePositionWithStopLoss_WhenCalculateFromPositions_ThenCalculatesHeat()
    {
        var positions = new[]
        {
            new PositionDto
            {
                Asset = "BTC",
                Size = 0.1m,
                EntryPrice = 50_000m,
                StopLossPrice = 49_000m,
                MarginUsed = 500m
            }
        };

        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 10_000m, 6m);

        result.HeatUsd.Should().Be(100m);
        result.HeatPercent.Should().Be(1m);
        result.Entries.Should().ContainSingle();
        result.Entries[0].Symbol.Should().Be("BTC");
        result.Entries[0].RiskUsd.Should().Be(100m);
        result.Entries[0].RiskPercent.Should().Be(1m);
    }

    [TestMethod]
    public void GivenMixedPositions_WhenCalculateFromPositions_ThenCalculatesCombinedHeat()
    {
        var positions = new[]
        {
            new PositionDto
            {
                Asset = "BTC",
                Size = 0.1m,
                EntryPrice = 50_000m,
                StopLossPrice = 49_000m,
                MarginUsed = 500m
            },
            new PositionDto
            {
                Asset = "ETH",
                Size = 2m,
                EntryPrice = 3_000m,
                MarginUsed = 120m
            }
        };

        var result = PortfolioHeatCalculator.CalculateFromPositions(positions, 10_000m, 2m);

        result.HeatUsd.Should().Be(220m);
        result.HeatPercent.Should().Be(2.2m);
        result.Entries.Should().HaveCount(2);
        result.IsLimitExceeded.Should().BeTrue();
    }

    [TestMethod]
    public void GivenTrackedRisks_WhenCalculateFromTrackedRisks_ThenBuildsResult()
    {
        var trackedRisks = new Dictionary<string, decimal>
        {
            ["BTC"] = 100m,
            ["ETH"] = 50m,
            ["SOL"] = 0m
        };

        var result = PortfolioHeatCalculator.CalculateFromTrackedRisks(trackedRisks, 10_000m, 6m);

        result.HeatUsd.Should().Be(150m);
        result.HeatPercent.Should().Be(1.5m);
        result.Entries.Should().HaveCount(2);
        result.Entries.Select(entry => entry.Symbol).Should().BeEquivalentTo(["BTC", "ETH"]);
    }

    [TestMethod]
    public void GivenRisksAndEquity_WhenCalculateHeatPercent_ThenReturnsAggregatePercent()
    {
        var heatPercent = PortfolioHeatCalculator.CalculateHeatPercent([100m, 50m, 25m], 10_000m);

        heatPercent.Should().Be(1.75m);
    }

    [TestMethod]
    public void GivenNoRisks_WhenCalculateHeatPercent_ThenReturnsZero()
    {
        var heatPercent = PortfolioHeatCalculator.CalculateHeatPercent([], 10_000m);

        heatPercent.Should().Be(0m);
    }

    [TestMethod]
    public void GivenZeroEquity_WhenCalculateHeatPercent_ThenReturnsZero()
    {
        var heatPercent = PortfolioHeatCalculator.CalculateHeatPercent([100m], 0m);

        heatPercent.Should().Be(0m);
    }
}