using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting.Models;

[TestClass]
public sealed class BacktestSummaryForReviewTests
{
    [TestMethod]
    public void GivenCompletedRun45Days_WhenFromBacktestRun_ThenDataQualityIsReliable()
    {
        var run = CreateCompletedBacktestRun(durationDays: 45);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.DataQuality.Should().Be("reliable");
        summary.DurationDays.Should().Be(45);
    }

    [TestMethod]
    public void GivenCompletedRun20Days_WhenFromBacktestRun_ThenDataQualityIsLimited()
    {
        var run = CreateCompletedBacktestRun(durationDays: 20);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.DataQuality.Should().Be("limited");
    }

    [TestMethod]
    public void GivenCompletedRun7Days_WhenFromBacktestRun_ThenDataQualityIsInsufficient()
    {
        var run = CreateCompletedBacktestRun(durationDays: 7);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.DataQuality.Should().Be("insufficient");
    }

    [TestMethod]
    public void GivenQueuedRun_WhenFromBacktestRun_ThenReturnsNull()
    {
        var run = BacktestRun.CreateQueued(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds(),
            endDateUtc: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().BeNull();
    }

    [TestMethod]
    public void GivenCompletedRun_WhenFromBacktestRun_ThenMetricsAreMapped()
    {
        var run = CreateCompletedBacktestRun(durationDays: 30);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.TotalTrades.Should().Be(47);
        summary.WinRate.Should().Be(55.3m);
        summary.TotalPnL.Should().Be(234.56m);
        summary.AverageTradePnL.Should().Be(4.99m);
        summary.TotalFeesPaid.Should().Be(12.34m);
        summary.InitialCapital.Should().Be(10000m);
        summary.MaxDrawdownAbsolute.Should().Be(150m);
        summary.MaxDrawdownPercent.Should().Be(1.5m);
    }

    [TestMethod]
    public void GivenCompletedRunWithEquitySeries_WhenFromBacktestRun_ThenFinalEquityFromLastSnapshot()
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var durationDays = 30;
        var endDate = startDate.AddDays(durationDays);

        var equitySeries = new List<EquitySnapshot>
        {
            new(startDate.ToUnixTimeMilliseconds(), 10000m),
            new(startDate.AddDays(15).ToUnixTimeMilliseconds(), 10200m),
            new(endDate.ToUnixTimeMilliseconds(), 10500m),
        };
        var equityJson = System.Text.Json.JsonSerializer.Serialize(equitySeries);

        var run = BacktestRun.Create(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: startDate.ToUnixTimeMilliseconds(),
            endDateUtc: endDate.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m,
            candlesReplayed: 2880,
            elapsedMs: 1500,
            totalTrades: 10,
            winningTrades: 6,
            losingTrades: 4,
            winRate: 60m,
            totalPnl: 500m,
            maxDrawdown: 100m,
            averageTradePnl: 50m,
            averageHoldTimeMinutes: 120,
            hedgesOpened: 0,
            totalFeesPaid: 5m,
            tradesJson: "[]",
            equityTimeSeriesJson: equityJson);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.FinalEquity.Should().Be(10500m);
        summary.ReturnPercent.Should().Be(5m);
    }

    [TestMethod]
    public void GivenRisingEquityCurve_WhenSummarize_ThenDescribesUpward()
    {
        var series = Enumerable.Range(0, 100)
            .Select(i => new EquitySnapshot(i * 900000L, 10000m + i * 10m))
            .ToList();

        var result = BacktestSummaryForReview.SummarizeEquityCurve(series, 10000m);

        result.Should().ContainAny("rising", "upward");
    }

    [TestMethod]
    public void GivenDecliningEquityCurve_WhenSummarize_ThenDescribesDecline()
    {
        var series = Enumerable.Range(0, 100)
            .Select(i => new EquitySnapshot(i * 900000L, 10000m - i * 20m))
            .ToList();

        var result = BacktestSummaryForReview.SummarizeEquityCurve(series, 10000m);

        result.Should().Contain("eclin");
    }

    [TestMethod]
    public void GivenTooFewDataPoints_WhenSummarize_ThenReturnsInsufficientMessage()
    {
        var series = new List<EquitySnapshot>
        {
            new(1000L, 10000m),
            new(2000L, 10100m),
        };

        var result = BacktestSummaryForReview.SummarizeEquityCurve(series, 10000m);

        result.Should().Contain("Insufficient");
    }

    private static BacktestRun CreateCompletedBacktestRun(int durationDays)
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = startDate.AddDays(durationDays);

        var equitySeries = Enumerable.Range(0, durationDays * 96)
            .Select(i => new EquitySnapshot(
                startDate.AddMinutes(i * 15).ToUnixTimeMilliseconds(),
                10000m + i * 0.5m))
            .ToList();
        var equityJson = System.Text.Json.JsonSerializer.Serialize(equitySeries);

        return BacktestRun.Create(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: startDate.ToUnixTimeMilliseconds(),
            endDateUtc: endDate.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m,
            candlesReplayed: durationDays * 96,
            elapsedMs: 1500,
            totalTrades: 47,
            winningTrades: 26,
            losingTrades: 21,
            winRate: 55.3m,
            totalPnl: 234.56m,
            maxDrawdown: 150m,
            averageTradePnl: 4.99m,
            averageHoldTimeMinutes: 252.0,
            hedgesOpened: 0,
            totalFeesPaid: 12.34m,
            tradesJson: "[]",
            equityTimeSeriesJson: equityJson);
    }
}
