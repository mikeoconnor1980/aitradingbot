using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Backtesting.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestMetricsCalculatorTests
{
    private BacktestMetricsCalculator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BacktestMetricsCalculator();
    }

    [TestMethod]
    public void GivenCompletedTradesAndEquityCurve_WhenCalculate_ThenReturnsExpectedSummaryMetrics()
    {
        var tradeLog = new List<BacktestTrade>
        {
            CreateTrade("trade-1", TradeType.GridFill, entryTimeUtc: 0, exitTimeUtc: 3_600_000, pnl: 100m, fees: 2m),
            CreateTrade("trade-2", TradeType.TakeProfit, entryTimeUtc: 7_200_000, exitTimeUtc: 14_400_000, pnl: -40m, fees: 1.5m),
            CreateTrade("trade-3", TradeType.HedgeOpen, entryTimeUtc: 21_600_000, exitTimeUtc: null, pnl: null, fees: 0.5m)
        };

        var equityCurve = new List<EquitySnapshot>
        {
            new(0, 10_000m),
            new(1, 10_200m),
            new(2, 10_050m),
            new(3, 10_500m),
            new(4, 9_900m),
            new(5, 10_060m)
        };

        var result = _sut.Calculate(tradeLog, equityCurve, initialCapital: 10_000m, gridCycles: 4);

        result.TotalTrades.Should().Be(2);
        result.WinningTrades.Should().Be(1);
        result.LosingTrades.Should().Be(1);
        result.WinRate.Should().Be(50m);
        result.TotalPnL.Should().Be(60m);
        result.MaxDrawdownAbsolute.Should().Be(600m);
        result.MaxDrawdownPercent.Should().BeApproximately(5.71m, 0.01m);
        result.AverageTradePnL.Should().Be(30m);
        result.AverageHoldTime.Should().Be(TimeSpan.FromHours(1.5));
        result.HedgesOpened.Should().Be(1);
        result.TotalFeesPaid.Should().Be(4m);
        result.GridCycles.Should().Be(4);
        result.FinalEquity.Should().Be(10_060m);
        result.TradeLog.Should().HaveCount(3);
        result.EquityTimeSeries.Should().HaveCount(6);
    }

    [TestMethod]
    public void GivenNoCompletedTradesOrEquityCurve_WhenCalculate_ThenReturnsZeroMetricsAndInitialCapital()
    {
        var tradeLog = new List<BacktestTrade>
        {
            CreateTrade("trade-1", TradeType.HedgeOpen, entryTimeUtc: 0, exitTimeUtc: null, pnl: null, fees: 0.25m)
        };

        var result = _sut.Calculate(tradeLog, [], initialCapital: 5_000m, gridCycles: 0);

        result.TotalTrades.Should().Be(0);
        result.WinningTrades.Should().Be(0);
        result.LosingTrades.Should().Be(0);
        result.WinRate.Should().Be(0m);
        result.TotalPnL.Should().Be(0m);
        result.MaxDrawdownAbsolute.Should().Be(0m);
        result.MaxDrawdownPercent.Should().Be(0m);
        result.AverageTradePnL.Should().Be(0m);
        result.AverageHoldTime.Should().Be(TimeSpan.Zero);
        result.HedgesOpened.Should().Be(1);
        result.TotalFeesPaid.Should().Be(0.25m);
        result.FinalEquity.Should().Be(5_000m);
    }

    [TestMethod]
    public void GivenMultipleDrawdowns_WhenCalculate_ThenUsesLargestPeakToTroughDecline()
    {
        var equityCurve = new List<EquitySnapshot>
        {
            new(0, 1_000m),
            new(1, 1_200m),
            new(2, 1_100m),
            new(3, 1_400m),
            new(4, 900m),
            new(5, 1_050m)
        };

        var result = _sut.Calculate([], equityCurve, initialCapital: 1_000m, gridCycles: 0);

        result.MaxDrawdownAbsolute.Should().Be(500m);
        result.MaxDrawdownPercent.Should().BeApproximately(35.71m, 0.01m);
    }

    [TestMethod]
    public void GivenRTrackedTrades_WhenCalculate_ThenReturnsExpectedAggregateRMetrics()
    {
        var tradeLog = CreateRTrackedTrades([2.1m, -1.0m, 1.5m, -1.0m, 3.0m, -0.8m, 2.0m, -1.0m, 1.8m, -1.0m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 2);

        result.Expectancy.Should().BeApproximately(0.56m, 0.01m);
        result.RWinRate.Should().Be(50m);
        result.ProfitFactor.Should().BeApproximately(2.17m, 0.01m);
        result.Sqn.Should().NotBeNull();
        result.AvgWinR.Should().BeApproximately(2.08m, 0.01m);
        result.AvgLossR.Should().BeApproximately(-0.96m, 0.01m);
        result.RDistribution.Should().Equal([2.1m, -1.0m, 1.5m, -1.0m, 3.0m, -0.8m, 2.0m, -1.0m, 1.8m, -1.0m]);
        result.WinLossRRatio.Should().BeApproximately(2.1667m, 0.0001m);
        result.KellyPercent.Should().BeApproximately(0.2692m, 0.0001m);
        result.HalfKellyPercent.Should().BeApproximately(0.1346m, 0.0001m);
    }

    [TestMethod]
    public void GivenNoRTrackedTrades_WhenCalculate_ThenReturnsNullRMetrics()
    {
        var tradeLog = new List<BacktestTrade>
        {
            CreateTrade("trade-1", TradeType.GridFill, entryTimeUtc: 0, exitTimeUtc: 1_000, pnl: 10m, fees: 1m),
            CreateTrade("trade-2", TradeType.TakeProfit, entryTimeUtc: 2_000, exitTimeUtc: 3_000, pnl: -5m, fees: 1m)
        };

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 0);

        result.Expectancy.Should().BeNull();
        result.ProfitFactor.Should().BeNull();
        result.Sqn.Should().BeNull();
        result.AvgWinR.Should().BeNull();
        result.AvgLossR.Should().BeNull();
        result.RWinRate.Should().BeNull();
        result.RDistribution.Should().BeNull();
        result.KellyPercent.Should().BeNull();
        result.HalfKellyPercent.Should().BeNull();
        result.WinLossRRatio.Should().BeNull();
    }

    [TestMethod]
    public void GivenSingleRTrackedTrade_WhenCalculate_ThenSqnIsNull()
    {
        var tradeLog = CreateRTrackedTrades([1.5m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 1);

        result.Expectancy.Should().Be(1.5m);
        result.Sqn.Should().BeNull();
        result.KellyPercent.Should().BeNull();
        result.HalfKellyPercent.Should().BeNull();
        result.WinLossRRatio.Should().BeNull();
    }

    [TestMethod]
    public void GivenRTrackedTradesWithKnownRatio_WhenCalculate_ThenKellyPercentIsCorrect()
    {
        var tradeLog = CreateRTrackedTrades([2.0m, 2.0m, 2.0m, -1.0m, -1.0m, 2.0m, 2.0m, 2.0m, -1.0m, -1.0m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 2);

        result.WinLossRRatio.Should().Be(2.0m);
        result.KellyPercent.Should().Be(0.4m);
        result.HalfKellyPercent.Should().Be(0.2m);
    }

    [TestMethod]
    public void GivenLosingSystem_WhenCalculate_ThenKellyPercentIsNegative()
    {
        var tradeLog = CreateRTrackedTrades([1.0m, -1.0m, -1.0m, -1.0m, -1.0m, 1.0m, -1.0m, -1.0m, 1.0m, -1.0m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 2);

        result.KellyPercent.Should().BeApproximately(-0.4m, 0.0001m);
        result.HalfKellyPercent.Should().BeApproximately(-0.2m, 0.0001m);
    }

    [TestMethod]
    public void GivenAllWinningTrades_WhenCalculate_ThenKellyAndWinLossRRatioAreNull()
    {
        var tradeLog = CreateRTrackedTrades([1.5m, 2.0m, 3.0m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 1);

        result.AvgLossR.Should().BeNull();
        result.WinLossRRatio.Should().BeNull();
        result.KellyPercent.Should().BeNull();
        result.HalfKellyPercent.Should().BeNull();
    }

    [TestMethod]
    public void GivenAllLosingTrades_WhenCalculate_ThenKellyAndWinLossRRatioAreNull()
    {
        var tradeLog = CreateRTrackedTrades([-1.0m, -0.8m, -1.2m]);

        var result = _sut.Calculate(tradeLog, CreateEquityCurve(), initialCapital: 10_000m, gridCycles: 1);

        result.AvgWinR.Should().BeNull();
        result.WinLossRRatio.Should().BeNull();
        result.KellyPercent.Should().BeNull();
        result.HalfKellyPercent.Should().BeNull();
    }

    private static BacktestTrade CreateTrade(
        string tradeId,
        TradeType tradeType,
        long entryTimeUtc,
        long? exitTimeUtc,
        decimal? pnl,
        decimal fees,
        decimal? initialRDollars = null,
        decimal? rMultipleResult = null)
    {
        return new BacktestTrade
        {
            TradeId = tradeId,
            GridCycleId = "grid-1",
            EntryTimeUtc = entryTimeUtc,
            EntryPrice = 100m,
            ExitTimeUtc = exitTimeUtc,
            ExitPrice = exitTimeUtc.HasValue ? 105m : null,
            Side = OrderSide.Buy,
            Size = 1m,
            PnL = pnl,
            Fees = fees,
            TradeType = tradeType,
            InitialRDollars = initialRDollars,
            RMultipleResult = rMultipleResult
        };
    }

    private static List<BacktestTrade> CreateRTrackedTrades(decimal[] rMultiples)
    {
        return rMultiples
            .Select((rMultiple, index) => CreateTrade(
                tradeId: $"trade-{index + 1}",
                tradeType: TradeType.GridFill,
                entryTimeUtc: index * 1_000L,
                exitTimeUtc: (index * 1_000L) + 500L,
                pnl: rMultiple * 100m,
                fees: 1m,
                initialRDollars: 100m,
                rMultipleResult: rMultiple))
            .ToList();
    }

    private static List<EquitySnapshot> CreateEquityCurve()
    {
        return
        [
            new EquitySnapshot(0, 10_000m),
            new EquitySnapshot(1, 10_050m),
            new EquitySnapshot(2, 10_120m)
        ];
    }
}