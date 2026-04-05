using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Optimization.Models;
using TradingApp.Application.Optimization.Services;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Optimization;

[TestClass]
public sealed class FitnessScorerTests
{
    private readonly FitnessScorer _scorer = new();

    [TestMethod]
    public void GivenResultBelowMinWinRate_WhenIsQualified_ThenReturnsFalse()
    {
        var result = CreateResult(winRate: 35m);

        var qualified = _scorer.IsQualified(result, new FitnessThresholds(), 10_000m);

        qualified.Should().BeFalse();
    }

    [TestMethod]
    public void GivenResultBelowMinTrades_WhenIsQualified_ThenReturnsFalse()
    {
        var result = CreateResult(totalTrades: 8);

        var qualified = _scorer.IsQualified(result, new FitnessThresholds(), 10_000m);

        qualified.Should().BeFalse();
    }

    [TestMethod]
    public void GivenResultExceedsMaxDrawdown_WhenIsQualified_ThenReturnsFalse()
    {
        var result = CreateResult(maxDrawdownAbsolute: 3_500m);

        var qualified = _scorer.IsQualified(result, new FitnessThresholds(), 10_000m);

        qualified.Should().BeFalse();
    }

    [TestMethod]
    public void GivenResultMeetsAllThresholds_WhenIsQualified_ThenReturnsTrue()
    {
        var result = CreateResult();

        var qualified = _scorer.IsQualified(result, new FitnessThresholds(), 10_000m);

        qualified.Should().BeTrue();
    }

    [TestMethod]
    public void GivenZeroTrades_WhenScore_ThenReturnsMinValue()
    {
        var result = CreateResult(totalTrades: 0, winningTrades: 0, losingTrades: 0);

        var score = _scorer.Score(result);

        score.Should().Be(decimal.MinValue);
    }

    [TestMethod]
    public void GivenPositivePnlLowDrawdown_WhenScore_ThenReturnsPositive()
    {
        var result = CreateResult(totalPnl: 1_000m, maxDrawdownAbsolute: 200m);

        var score = _scorer.Score(result);

        score.Should().BePositive();
    }

    [TestMethod]
    public void GivenNegativePnl_WhenScore_ThenReturnsNegative()
    {
        var result = CreateResult(totalPnl: -100m, maxDrawdownAbsolute: 50m);

        var score = _scorer.Score(result);

        score.Should().BeNegative();
    }

    [TestMethod]
    public void GivenZeroDrawdown_WhenScore_ThenUsesEpsilon()
    {
        var result = CreateResult(totalPnl: 100m, maxDrawdownAbsolute: 0m, totalTrades: 16);

        var score = _scorer.Score(result);

        score.Should().Be(40_000m);
    }

    [TestMethod]
    public void GivenHigherPnlSameDrawdown_WhenScore_ThenHigherScore()
    {
        var lower = CreateResult(totalPnl: 500m, maxDrawdownAbsolute: 200m);
        var higher = CreateResult(totalPnl: 750m, maxDrawdownAbsolute: 200m);

        _scorer.Score(higher).Should().BeGreaterThan(_scorer.Score(lower));
    }

    [TestMethod]
    public void GivenMoreTradesSamePnl_WhenScore_ThenHigherScore()
    {
        var fewer = CreateResult(totalTrades: 9);
        var more = CreateResult(totalTrades: 25);

        _scorer.Score(more).Should().BeGreaterThan(_scorer.Score(fewer));
    }

    private static BacktestResult CreateResult(
        int totalTrades = 20,
        int winningTrades = 12,
        int losingTrades = 8,
        decimal winRate = 60m,
        decimal totalPnl = 800m,
        decimal maxDrawdownAbsolute = 1_500m)
    {
        return new BacktestResult
        {
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = winRate,
            TotalPnL = totalPnl,
            MaxDrawdownAbsolute = maxDrawdownAbsolute,
            MaxDrawdownPercent = 15m,
            AverageTradePnL = totalTrades == 0 ? 0m : totalPnl / totalTrades,
            AverageHoldTime = TimeSpan.FromMinutes(30),
            HedgesOpened = 0,
            TotalFeesPaid = 10m,
            GridCycles = 0,
            CandlesReplayed = 1000,
            FinalEquity = 10_800m,
            EquityTimeSeries = [],
            TradeLog = [],
        };
    }
}