using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Optimization.Models;
using TradePilot.Application.Optimization.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Optimization;

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

        // Base score = (100 / 0.01) * sqrt(16) = 10000 * 4 = 40000
        // Empty TradeLog => Sharpe=0, ProfitFactor=0 => raw bonus = 0 + (-1) = -1
        // confidence = sqrt(16)/sqrt(20) ≈ 0.894 => scaled bonus ≈ -0.894
        score.Should().BeApproximately(39_999.1m, 0.1m);
    }

    [TestMethod]
    public void GivenHigherPnlSameDrawdown_WhenScore_ThenHigherScore()
    {
        var lower = CreateResult(totalPnl: 500m, maxDrawdownAbsolute: 200m);
        var higher = CreateResult(totalPnl: 750m, maxDrawdownAbsolute: 200m);

        _scorer.Score(higher).Should().BeGreaterThan(_scorer.Score(lower));
    }

    [TestMethod]
    public void GivenFewTradesInflatedMetrics_WhenScore_ThenConfidenceScalesDownBonuses()
    {
        // With few trades, metric bonuses (Sharpe/PF) are statistically unreliable.
        // The confidence factor dampens them: sqrt(trades)/sqrt(20).
        // All-winning trades: Sharpe is high, PF is capped at 100.
        var fewAllWinning = CreateResultWithTrades([200m, 150m, 180m, 170m]);
        var manyAllWinning = CreateResultWithTrades([
            200m, 150m, 180m, 170m, 190m, 160m, 210m, 140m, 195m, 175m,
            200m, 150m, 180m, 170m, 190m, 160m, 210m, 140m, 195m, 175m,
        ]);

        var fewScore = _scorer.Score(fewAllWinning);
        var manyScore = _scorer.Score(manyAllWinning);

        // Both have similar per-trade metrics (all winners, high Sharpe/PF).
        // Many trades gets full confidence (1.0) on bonuses + higher sqrt(n) tradeFactor.
        // Few trades gets dampened confidence (~0.447) on bonuses.
        manyScore.Should().BeGreaterThan(fewScore);
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

    [TestMethod]
    public void GivenTradesWithMixedPnl_WhenComputeMetrics_ThenReturnsValidMetrics()
    {
        var result = CreateResultWithTrades([100m, -50m, 200m, -30m, 150m]);

        var metrics = _scorer.ComputeMetrics(result);

        metrics.SharpeRatio.Should().BeGreaterThan(0m);
        metrics.SortinoRatio.Should().BeGreaterThan(0m);
        metrics.ProfitFactor.Should().BeGreaterThan(1m);
        metrics.CalmarRatio.Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public void GivenNoTrades_WhenComputeMetrics_ThenReturnsZeroMetrics()
    {
        var result = CreateResult(totalTrades: 0, winningTrades: 0, losingTrades: 0);

        var metrics = _scorer.ComputeMetrics(result);

        metrics.SharpeRatio.Should().Be(0m);
        metrics.SortinoRatio.Should().Be(0m);
        metrics.ProfitFactor.Should().Be(0m);
    }

    [TestMethod]
    public void GivenAllProfitableTrades_WhenComputeMetrics_ThenProfitFactorIsCapped()
    {
        var result = CreateResultWithTrades([100m, 200m, 300m]);

        var metrics = _scorer.ComputeMetrics(result);

        metrics.ProfitFactor.Should().Be(100m);
        metrics.CalmarRatio.Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public void GivenAllLosingTrades_WhenComputeMetrics_ThenProfitFactorIsZero()
    {
        var result = CreateResultWithTrades([-100m, -200m, -50m]);

        var metrics = _scorer.ComputeMetrics(result);

        metrics.ProfitFactor.Should().Be(0m);
        metrics.SharpeRatio.Should().BeLessThan(0m);
    }

    private static BacktestResult CreateResultWithTrades(decimal[] tradePnls)
    {
        var tradeLog = tradePnls.Select((pnl, i) => new BacktestTrade
        {
            TradeId = $"t-{i}",
            GridCycleId = "gc-1",
            EntryTimeUtc = 1000 + i,
            EntryPrice = 50000m,
            ExitTimeUtc = 2000 + i,
            ExitPrice = 50000m + pnl,
            Side = OrderSide.Buy,
            Size = 1m,
            PnL = pnl,
            Fees = 0.5m,
            TradeType = TradeType.SignalEntry,
        }).ToArray();

        var totalPnl = tradePnls.Sum();

        return new BacktestResult
        {
            TotalTrades = tradePnls.Length,
            WinningTrades = tradePnls.Count(p => p > 0),
            LosingTrades = tradePnls.Count(p => p < 0),
            WinRate = tradePnls.Length > 0 ? (decimal)tradePnls.Count(p => p > 0) / tradePnls.Length * 100m : 0m,
            TotalPnL = totalPnl,
            MaxDrawdownAbsolute = Math.Abs(tradePnls.Where(p => p < 0).DefaultIfEmpty(0m).Min()),
            MaxDrawdownPercent = 5m,
            AverageTradePnL = tradePnls.Length > 0 ? totalPnl / tradePnls.Length : 0m,
            AverageHoldTime = TimeSpan.FromMinutes(30),
            HedgesOpened = 0,
            TotalFeesPaid = tradePnls.Length * 0.5m,
            GridCycles = 0,
            CandlesReplayed = 1000,
            FinalEquity = 10_000m + totalPnl,
            EquityTimeSeries = [],
            TradeLog = tradeLog,
        };
    }
}