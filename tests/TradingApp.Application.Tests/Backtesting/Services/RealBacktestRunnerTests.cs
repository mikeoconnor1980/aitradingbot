using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class RealBacktestRunnerTests
{
    private const long FifteenMinutesMs = 15L * 60L * 1000L;
    private const long OneHourMs = 60L * 60L * 1000L;
    private const long FourHoursMs = 4L * 60L * 60L * 1000L;

    private Mock<ICandleRepository> _candleRepositoryMock = default!;
    private BacktestExecutionContextAccessor _executionContextAccessor = default!;
    private BacktestRunner _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _candleRepositoryMock = new Mock<ICandleRepository>();
        _executionContextAccessor = new BacktestExecutionContextAccessor();

        _sut = new BacktestRunner(
            _candleRepositoryMock.Object,
            new BacktestMarketContextBuilder(),
            new GridStrategyEngine(),
            new GridController(),
            new PassThroughRiskEngine(),
            new BacktestPositionManager(_executionContextAccessor),
            _executionContextAccessor);
    }

    [TestMethod]
    public async Task GivenDeterministicCandles_WhenRunAsync_ThenProducesCompletedTrade()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (4 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = 2,
            StrategyConfigJson = "{\"gridLevels\":1,\"gridSpacing\":0.5,\"takeProfitPercent\":1,\"breakdownThreshold\":2,\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0,\"positionSize\":100,\"leverage\":3,\"stopLossPercent\":5}"
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.7m, 101.2m, 99.6m, 100.8m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 100.9m, 101.4m, 100.7m, 101.1m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 99m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 99m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        result.TotalTrades.Should().BeGreaterThan(0);
        result.WinningTrades.Should().BeGreaterThan(0);
        result.TotalPnL.Should().BeGreaterThan(0m);
        result.TradeLog.Should().ContainSingle(trade => trade.ExitTimeUtc.HasValue);
    }

    [TestMethod]
    public async Task GivenAuditLogEnabled_WhenRunCompletes_ThenAuditDataIsCaptured()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (4 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = 2,
            StrategyConfigJson = "{\"gridLevels\":1,\"entryMode\":\"WaitForLimitPrice\",\"manualAnchorPrice\":100.2,\"gridSpacing\":0.5,\"takeProfitPercent\":1,\"breakdownThreshold\":2,\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0,\"positionSize\":100,\"leverage\":3,\"stopLossPercent\":5}",
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.7m, 101.2m, 99.6m, 100.8m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 100.9m, 101.4m, 100.7m, 101.1m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 99m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 99m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        result.CandleEvaluationLog.Should().NotBeNull();
        result.CandleEvaluationLog.Should().NotBeEmpty();
        result.CandleEvaluationLog!.Any(entry => entry.IsWarmup).Should().BeTrue();
        result.CandleEvaluationLog!.Any(entry => !entry.IsWarmup).Should().BeTrue();
        result.OrderEventLog.Should().NotBeNull();
        result.OrderEventLog.Should().NotBeEmpty();
        result.GridCycleLog.Should().NotBeNull();
        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].AnchorPrice.Should().Be(100.2m);
        result.GridCycleLog![0].LevelsPlaced.Should().Be(1);
        result.GridCycleLog![0].LevelsFilled.Should().Be(1);
        result.GridCycleLog![0].StopLossPrice.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenInitialMarketThenGridEntryMode_WhenRunCompletes_ThenFirstTrancheOpensAtMarketAndCycleStillCloses()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (3 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = 2,
            StrategyConfigJson = "{\"gridLevels\":2,\"entryMode\":\"InitialMarketThenGrid\",\"gridSpacing\":0.5,\"takeProfitPercent\":1,\"breakdownThreshold\":2,\"makerFee\":0.0001,\"takerFee\":0.00035,\"slippage\":0,\"positionSize\":100,\"leverage\":3,\"stopLossPercent\":5}",
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.4m, 99.8m, 100.2m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 100.3m, 101.5m, 100.1m, 101.1m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 99m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 99m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        result.TotalTrades.Should().Be(1);
        result.TradeLog.Should().ContainSingle(trade => trade.ExitTimeUtc.HasValue);
        result.TradeLog[0].EntryPrice.Should().Be(100.2m);
        result.TradeLog[0].EntryTimeUtc.Should().Be(config.StartDateUtc + FifteenMinutesMs);
        result.TradeLog[0].TradeType.Should().Be(TradeType.GridFill);
        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsPlaced.Should().Be(2);
        result.GridCycleLog[0].LevelsFilled.Should().Be(1);
        result.OrderEventLog.Should().Contain(entry => entry.EventType == OrderEventType.Placed && entry.OrderType == OrderType.Market.ToString() && entry.Side == OrderSide.Buy.ToString());
    }

    private void SetupCandles(string interval, IReadOnlyList<Candle> candles)
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                It.IsAny<string>(),
                interval,
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);
    }

    private static Candle CreateCandle(string interval, long timestamp, decimal open, decimal high, decimal low, decimal close)
    {
        return Candle.Create(
            "Binance",
            "BTC",
            interval,
            timestamp,
            open,
            high,
            low,
            close,
            1_000m,
            10);
    }
}