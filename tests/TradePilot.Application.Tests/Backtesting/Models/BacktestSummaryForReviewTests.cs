using System.Text.Json;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Tests.Backtesting.Models;

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
        summary!.TotalTrades.Should().Be(6);
        summary.WinRate.Should().Be(66.7m);
        summary.TotalPnL.Should().Be(234.56m);
        summary.AverageTradePnL.Should().Be(4.99m);
        summary.TotalFeesPaid.Should().Be(12.34m);
        summary.InitialCapital.Should().Be(10000m);
        summary.MaxDrawdownAbsolute.Should().Be(150m);
        summary.MaxDrawdownPercent.Should().Be(1.5m);
    }

    [TestMethod]
    public void GivenCompletedRunWithTrades_WhenFromBacktestRun_ThenTradeMetricsAreComputed()
    {
        var run = CreateCompletedBacktestRun(durationDays: 30);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.WinningTrades.Should().Be(4);
        summary.LosingTrades.Should().Be(2);
        summary.ProfitFactor.Should().BeGreaterThan(0);
        summary.MaxConsecutiveLosses.Should().BeGreaterOrEqualTo(1);
        summary.AverageWinSize.Should().BeGreaterThan(0);
        summary.AverageLossSize.Should().BeGreaterThan(0);
        summary.RewardRiskRatio.Should().BeGreaterThan(0);
        summary.LargestWin.Should().BeGreaterThan(0);
        summary.LargestLoss.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenCompletedRunWithTrades_WhenFromBacktestRun_ThenSharpeRatioIsComputed()
    {
        var run = CreateCompletedBacktestRun(durationDays: 30);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.SharpeRatio.Should().NotBe(0);
    }

    [TestMethod]
    public void GivenCompletedRunWithTrades_WhenFromBacktestRun_ThenFeeRatioIsComputed()
    {
        var run = CreateCompletedBacktestRun(durationDays: 30);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.FeeToGrossProfitRatio.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenCompletedRunWithEquitySeries_WhenFromBacktestRun_ThenDrawdownEpisodesAreComputed()
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var durationDays = 30;
        var endDate = startDate.AddDays(durationDays);

        // Create equity series with a clear drawdown: rises to 10200, drops to 10000, recovers to 10500
        var equitySeries = new List<EquitySnapshot>
        {
            new(startDate.ToUnixTimeMilliseconds(), 10000m),
            new(startDate.AddDays(5).ToUnixTimeMilliseconds(), 10200m),
            new(startDate.AddDays(10).ToUnixTimeMilliseconds(), 10000m),
            new(startDate.AddDays(15).ToUnixTimeMilliseconds(), 10100m),
            new(startDate.AddDays(20).ToUnixTimeMilliseconds(), 10300m),
            new(endDate.ToUnixTimeMilliseconds(), 10500m),
        };
        var equityJson = JsonSerializer.Serialize(equitySeries);

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
            totalTrades: 4,
            winningTrades: 3,
            losingTrades: 1,
            winRate: 75m,
            totalPnl: 500m,
            maxDrawdown: 200m,
            averageTradePnl: 125m,
            averageHoldTimeMinutes: 120,
            hedgesOpened: 0,
            totalFeesPaid: 5m,
            tradesJson: "[]",
            equityTimeSeriesJson: equityJson);

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.TopDrawdownEpisodes.Should().NotBeEmpty();
        summary.TopDrawdownEpisodes[0].DepthPercent.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void GivenEmptyTradesJson_WhenFromBacktestRun_ThenTradeMetricsAreZero()
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = startDate.AddDays(30);

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
            totalTrades: 0,
            winningTrades: 0,
            losingTrades: 0,
            winRate: 0m,
            totalPnl: 0m,
            maxDrawdown: 0m,
            averageTradePnl: 0m,
            averageHoldTimeMinutes: 0,
            hedgesOpened: 0,
            totalFeesPaid: 0m,
            tradesJson: "[]",
            equityTimeSeriesJson: "[]");

        var summary = BacktestSummaryForReview.FromBacktestRun(run);

        summary.Should().NotBeNull();
        summary!.ProfitFactor.Should().Be(0);
        summary.SharpeRatio.Should().Be(0);
        summary.MaxConsecutiveLosses.Should().Be(0);
        summary.RewardRiskRatio.Should().Be(0);
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
    public void GivenCompletedRunWithAuditLog_WhenFromBacktestRun_ThenBuildsRegimeSegmentation()
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = startDate.AddDays(45);

        var candleLog = new List<CandleEvaluationEntry>
        {
            new()
            {
                TimestampUtc = startDate.AddHours(1).ToUnixTimeMilliseconds(),
                Open = 105m,
                High = 107m,
                Low = 104m,
                Close = 106m,
                Volume = 1200m,
                IsWarmup = false,
                EmaFast = 104m,
                EmaSlow = 100m,
                EmaTrend = 98m,
                Rsi = 58m,
                Atr = 1m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-asia"
            },
            new()
            {
                TimestampUtc = startDate.AddHours(10).ToUnixTimeMilliseconds(),
                Open = 100m,
                High = 100.5m,
                Low = 99.5m,
                Close = 100m,
                Volume = 1000m,
                IsWarmup = false,
                EmaFast = 100.2m,
                EmaSlow = 100.1m,
                EmaTrend = 100m,
                Rsi = 48m,
                Atr = 2m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-europe"
            },
            new()
            {
                TimestampUtc = startDate.AddHours(18).ToUnixTimeMilliseconds(),
                Open = 109m,
                High = 111m,
                Low = 108m,
                Close = 110m,
                Volume = 1500m,
                IsWarmup = false,
                EmaFast = 108m,
                EmaSlow = 100m,
                EmaTrend = 97m,
                Rsi = 63m,
                Atr = 4m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-us"
            }
        };

        var gridCycles = new List<GridCycleEntry>
        {
            new()
            {
                GridCycleId = "cycle-asia",
                DeployTimestampUtc = candleLog[0].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.5m, 99m],
                LevelsFilled = 2,
                TakeProfitPrice = 101m,
                StopLossPrice = 97m,
                ExitReason = "TakeProfit",
                CyclePnl = 40m,
                CycleDurationMs = (long)TimeSpan.FromHours(5).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(6).ToUnixTimeMilliseconds(),
            },
            new()
            {
                GridCycleId = "cycle-europe",
                DeployTimestampUtc = candleLog[1].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.7m, 99.2m],
                LevelsFilled = 3,
                TakeProfitPrice = 100.8m,
                StopLossPrice = 97m,
                ExitReason = "StopLoss",
                CyclePnl = -25m,
                CycleDurationMs = (long)TimeSpan.FromHours(7).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(17).ToUnixTimeMilliseconds(),
            },
            new()
            {
                GridCycleId = "cycle-us",
                DeployTimestampUtc = candleLog[2].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.4m, 98.8m],
                LevelsFilled = 4,
                TakeProfitPrice = 101.4m,
                StopLossPrice = 96.5m,
                ExitReason = "TakeProfit",
                CyclePnl = 70m,
                CycleDurationMs = (long)TimeSpan.FromHours(4).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(22).ToUnixTimeMilliseconds(),
            }
        };

        var fundingRates = new List<FundingRate>
        {
            FundingRate.Create("ETH", startDate.ToUnixTimeMilliseconds(), -0.0002m, 100m),
            FundingRate.Create("ETH", startDate.AddHours(8).ToUnixTimeMilliseconds(), 0m, 100m),
            FundingRate.Create("ETH", startDate.AddHours(16).ToUnixTimeMilliseconds(), 0.0002m, 100m),
        };

        var run = BacktestRun.Create(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: startDate.ToUnixTimeMilliseconds(),
            endDateUtc: endDate.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m,
            candlesReplayed: 45 * 96,
            elapsedMs: 1500,
            totalTrades: 18,
            winningTrades: 11,
            losingTrades: 7,
            winRate: 61.1m,
            totalPnl: 85m,
            maxDrawdown: 140m,
            averageTradePnl: 4.72m,
            averageHoldTimeMinutes: 240d,
            hedgesOpened: 1,
            totalFeesPaid: 16m,
            tradesJson: "[]",
            equityTimeSeriesJson: JsonSerializer.Serialize(new List<EquitySnapshot>
            {
                new(startDate.ToUnixTimeMilliseconds(), 10000m),
                new(endDate.ToUnixTimeMilliseconds(), 10085m),
            }),
            candleLogJson: JsonSerializer.Serialize(candleLog),
            gridCycleLogJson: JsonSerializer.Serialize(gridCycles));

        var summary = BacktestSummaryForReview.FromBacktestRun(run, fundingRates);

        summary.Should().NotBeNull();
        summary!.RegimeSegmentation.Should().NotBeNull();
        summary.RegimeSegmentation!.CompletedGridCyclesAnalysed.Should().Be(3);
        summary.RegimeSegmentation.TrendSegments.Should().ContainSingle(stat => stat.Segment == "Trending" && stat.CycleCount == 2);
        summary.RegimeSegmentation.TrendSegments.Should().ContainSingle(stat => stat.Segment == "Ranging" && stat.CycleCount == 1);
        summary.RegimeSegmentation.FundingSegments.Should().ContainSingle(stat => stat.Segment == "Strongly Negative Funding" && stat.CycleCount == 1);
        summary.RegimeSegmentation.SessionSegments.Should().ContainSingle(stat => stat.Segment == "Europe Session" && stat.CycleCount == 1);
        summary.RegimeSegmentation.OpenInterestTrendNote.Should().ContainEquivalentOf("open-interest");
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
        var equityJson = JsonSerializer.Serialize(equitySeries);

        var trades = new List<BacktestTrade>
        {
            new() { TradeId = "t1", GridCycleId = "g1", EntryTimeUtc = startDate.AddHours(1).ToUnixTimeMilliseconds(), EntryPrice = 3000m, ExitTimeUtc = startDate.AddHours(5).ToUnixTimeMilliseconds(), ExitPrice = 3100m, Side = OrderSide.Buy, Size = 1m, PnL = 100m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t2", GridCycleId = "g1", EntryTimeUtc = startDate.AddHours(10).ToUnixTimeMilliseconds(), EntryPrice = 3100m, ExitTimeUtc = startDate.AddHours(14).ToUnixTimeMilliseconds(), ExitPrice = 3050m, Side = OrderSide.Buy, Size = 1m, PnL = -50m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t3", GridCycleId = "g2", EntryTimeUtc = startDate.AddDays(2).ToUnixTimeMilliseconds(), EntryPrice = 3050m, ExitTimeUtc = startDate.AddDays(2).AddHours(6).ToUnixTimeMilliseconds(), ExitPrice = 3150m, Side = OrderSide.Buy, Size = 1m, PnL = 100m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t4", GridCycleId = "g2", EntryTimeUtc = startDate.AddDays(3).ToUnixTimeMilliseconds(), EntryPrice = 3150m, ExitTimeUtc = startDate.AddDays(3).AddHours(4).ToUnixTimeMilliseconds(), ExitPrice = 3200m, Side = OrderSide.Buy, Size = 1m, PnL = 50m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t5", GridCycleId = "g3", EntryTimeUtc = startDate.AddDays(5).ToUnixTimeMilliseconds(), EntryPrice = 3200m, ExitTimeUtc = startDate.AddDays(5).AddHours(8).ToUnixTimeMilliseconds(), ExitPrice = 3170m, Side = OrderSide.Buy, Size = 1m, PnL = -30m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t6", GridCycleId = "g3", EntryTimeUtc = startDate.AddDays(7).ToUnixTimeMilliseconds(), EntryPrice = 3170m, ExitTimeUtc = startDate.AddDays(7).AddHours(6).ToUnixTimeMilliseconds(), ExitPrice = 3250m, Side = OrderSide.Buy, Size = 1m, PnL = 80m, Fees = 2m, TradeType = TradeType.GridFill },
        };
        var tradesJson = JsonSerializer.Serialize(trades);

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
            totalTrades: 6,
            winningTrades: 4,
            losingTrades: 2,
            winRate: 66.7m,
            totalPnl: 234.56m,
            maxDrawdown: 150m,
            averageTradePnl: 4.99m,
            averageHoldTimeMinutes: 252.0,
            hedgesOpened: 0,
            totalFeesPaid: 12.34m,
            tradesJson: tradesJson,
            equityTimeSeriesJson: equityJson);
    }
}
