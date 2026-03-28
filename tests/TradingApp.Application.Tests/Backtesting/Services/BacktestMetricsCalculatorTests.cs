using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Backtesting.Services;

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

    private static BacktestTrade CreateTrade(
        string tradeId,
        TradeType tradeType,
        long entryTimeUtc,
        long? exitTimeUtc,
        decimal? pnl,
        decimal fees)
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
            TradeType = tradeType
        };
    }
}