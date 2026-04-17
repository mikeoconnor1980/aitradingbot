using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Backtesting.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Backtesting.Services;

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

        var conditionEvaluator = new ConditionEvaluator(
            [
                new RsiConditionHandler(),
                new PriceVsEmaConditionHandler(new Mock<ILogger<PriceVsEmaConditionHandler>>().Object)
            ],
            new Mock<ILogger<ConditionEvaluator>>().Object);

        _sut = new BacktestRunner(
            _candleRepositoryMock.Object,
            new BacktestMarketContextBuilder(),
            new CompositeStrategyEngine(
                new GridStrategyEngine(),
                conditionEvaluator,
                new TrendFilterEvaluator(new Mock<ILogger<TrendFilterEvaluator>>().Object)),
            new GridController(),
            new PassThroughRiskEngine(),
            new BacktestPositionManager(_executionContextAccessor),
            _executionContextAccessor,
            new SignalController());
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
            Strategy = CreateStrategyConfig(gridLevels: 1),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
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
            Strategy = CreateStrategyConfig(gridLevels: 1, entryMode: EntryModes.WaitForLimitPrice, manualAnchorPrice: 100.2m),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
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
            EndDateUtc = (12 * OneHourMs) + (4 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = CreateStrategyConfig(gridLevels: 2, entryMode: EntryModes.InitialMarketThenGrid),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.4m, 99.8m, 100.2m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 100.3m, 101.5m, 100.1m, 101.3m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 101.3m, 101.6m, 101.0m, 101.4m),
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
        result.GridCycleLog[0].ExitReason.Should().Be("TakeProfit");
        result.OrderEventLog.Should().Contain(entry => entry.EventType == OrderEventType.Placed && entry.OrderType == OrderType.Market.ToString() && entry.Side == OrderSide.Buy.ToString());
        result.OrderEventLog.Should().Contain(entry => entry.EventType == OrderEventType.Cancelled && entry.CancellationReason == CancellationReason.TakeProfitTriggered);
    }

    [TestMethod]
    public async Task GivenMultiLevelGrid_WhenPartialFillsAccumulate_ThenLadderRemainsActiveUntilTakeProfitClosesCycle()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (7 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = CreateStrategyConfig(gridLevels: 5),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.6m, 99.7m, 98.4m, 98.6m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 98.7m, 99.5m, 98.5m, 99.4m),
            CreateCandle("15m", config.StartDateUtc + (4 * FifteenMinutesMs), 99.4m, 100.5m, 99.3m, 100.2m),
            CreateCandle("15m", config.StartDateUtc + (5 * FifteenMinutesMs), 100.2m, 100.4m, 99.9m, 100.1m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 13 * OneHourMs, 99m, 101m, 98m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 98m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsPlaced.Should().Be(5);
        result.GridCycleLog[0].LevelsFilled.Should().Be(3);
        result.GridCycleLog[0].ExitReason.Should().Be("TakeProfit");
        result.TradeLog.Should().HaveCount(3);
        result.TradeLog.Should().OnlyContain(trade => trade.ExitTimeUtc.HasValue);
        result.TradeLog.Should().OnlyContain(trade => trade.TradeType == TradeType.GridFill);
        result.TotalPnL.Should().BeGreaterThan(0m);
        result.OrderEventLog!.Count(entry => entry.EventType == OrderEventType.Cancelled && entry.CancellationReason == CancellationReason.TakeProfitTriggered).Should().Be(2);
        result.OrderEventLog!.Count(entry => entry.EventType == OrderEventType.Filled && entry.Side == OrderSide.Buy.ToString()).Should().Be(3);
    }

    [TestMethod]
    public async Task GivenMultiLevelGrid_WhenStopLossTriggersAfterPartialFill_ThenRemainingLevelsAreCancelledAndCycleCloses()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (5 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = CreateStrategyConfig(gridLevels: 5, stopLossPercent: 0.4m),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.6m, 99.7m, 99.05m, 99.08m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 99.08m, 99.2m, 98.9m, 98.95m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 98.9m, 99.1m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 98.9m, 99.1m),
        ]);

        var result = await _sut.RunAsync(config);

        result.TotalPnL.Should().BeLessThan(0m);
        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsPlaced.Should().Be(5);
        result.GridCycleLog[0].LevelsFilled.Should().Be(1);
        result.GridCycleLog[0].ExitReason.Should().Be("StopLoss");
        result.GridCycleLog[0].StopLossPrice.Should().NotBeNull();
        result.OrderEventLog!.Count(entry => entry.EventType == OrderEventType.Cancelled && entry.CancellationReason == CancellationReason.StopLossTriggered).Should().Be(4);
        result.OrderEventLog.Should().Contain(entry => entry.EventType == OrderEventType.Placed && entry.OrderType == OrderType.Market.ToString() && entry.Side == OrderSide.Sell.ToString());
    }

    [TestMethod]
    public async Task GivenMultiLevelGrid_WhenAllLevelsFill_ThenControllerPlacesSingleLimitTakeProfitForTheFullPosition()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (5 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = CreateStrategyConfig(gridLevels: 3),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
            EnableAuditLog = true,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 98.4m, 98.8m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 98.8m, 100.5m, 98.7m, 100.1m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 100.1m, 100.4m, 99.9m, 100.2m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 98m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 98m, 100m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 98m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 101m, 98m, 100m),
        ]);

        var result = await _sut.RunAsync(config);

        result.GridCycleLog.Should().ContainSingle();
        result.GridCycleLog![0].LevelsPlaced.Should().Be(3);
        result.GridCycleLog[0].LevelsFilled.Should().Be(3);
        result.GridCycleLog[0].ExitReason.Should().Be("TakeProfit");
        result.GridCycleLog[0].StopLossPrice.Should().BeNull();
        result.OrderEventLog.Should().Contain(entry => entry.EventType == OrderEventType.Placed && entry.OrderType == OrderType.Limit.ToString() && entry.Side == OrderSide.Sell.ToString());
        result.OrderEventLog!.Count(entry => entry.EventType == OrderEventType.Filled && entry.Side == OrderSide.Buy.ToString()).Should().Be(3);
        result.TotalPnL.Should().BeGreaterThan(0m);
    }

    [TestMethod]
    public async Task GivenSignalModeStrategyWithPassingRsi_WhenRunAsync_ThenTradesRecorded()
    {
        var config = CreateSignalBacktestConfig(CreateSignalStrategyConfig(30m));
        SetupSignalModeCandles(config);

        var result = await _sut.RunAsync(config);

        result.TotalTrades.Should().BeGreaterThan(0);
        result.TradeLog.Should().ContainSingle(trade =>
            trade.TradeType == TradeType.SignalEntry &&
            trade.ExitTimeUtc.HasValue);
        result.GridCycles.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenSignalModeStrategyWithNonPassingRsi_WhenRunAsync_ThenNoTradesRecorded()
    {
        var config = CreateSignalBacktestConfig(CreateSignalStrategyConfig(0m));
        SetupSignalModeCandles(config);

        var result = await _sut.RunAsync(config);

        result.TotalTrades.Should().Be(0);
        result.TradeLog.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenRiskBasedRMultipleTakeProfit_WhenRunAsync_ThenClosedTradeIncludesRMetrics()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (4 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = CreateStrategyConfig(
                gridLevels: 1,
                takeProfitValue: 1m,
                stopLossPercent: 2m,
                takeProfitType: ExitRuleType.RMultiple,
                positionSizeType: PositionSizeType.RiskBased,
                riskPerTradePercent: 1m),
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 2,
        };

        SetupCandles("15m",
        [
            CreateCandle("15m", config.StartDateUtc - (2 * FifteenMinutesMs), 100m, 101m, 99.5m, 100m),
            CreateCandle("15m", config.StartDateUtc - FifteenMinutesMs, 100m, 100.5m, 99.8m, 100m),
            CreateCandle("15m", config.StartDateUtc, 100m, 100.2m, 99.9m, 100m),
            CreateCandle("15m", config.StartDateUtc + FifteenMinutesMs, 100m, 100.1m, 99.4m, 99.6m),
            CreateCandle("15m", config.StartDateUtc + (2 * FifteenMinutesMs), 99.7m, 102.4m, 99.2m, 102.1m),
            CreateCandle("15m", config.StartDateUtc + (3 * FifteenMinutesMs), 102.1m, 102.6m, 101.9m, 102.3m),
        ]);

        SetupCandles("1h",
        [
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 103m, 99m, 102m),
        ]);

        SetupCandles("4h",
        [
            CreateCandle("4h", OneHourMs * 4, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 8, 100m, 101m, 99m, 100m),
            CreateCandle("4h", OneHourMs * 12, 100m, 103m, 99m, 102m),
        ]);

        var result = await _sut.RunAsync(config);

        var trade = result.TradeLog.Should().ContainSingle(t => t.ExitTimeUtc.HasValue).Subject;
        trade.InitialRDollars.Should().Be(100m);
        trade.RMultipleResult.Should().Be(1m);
        trade.MFE.Should().NotBeNull();
        trade.MAE.Should().NotBeNull();
        trade.MFE.Should().BeGreaterThan(0m);
        trade.MAE.Should().BeLessThanOrEqualTo(0m);
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

    private static StrategyConfig CreateStrategyConfig(
        int gridLevels,
        decimal gridSpacing = 0.5m,
        decimal takeProfitValue = 1m,
        decimal breakdownThreshold = 2m,
        decimal positionSize = 100m,
        decimal stopLossPercent = 5m,
        string? entryMode = null,
        decimal? manualAnchorPrice = null,
        ExitRuleType takeProfitType = ExitRuleType.FixedPercent,
        PositionSizeType positionSizeType = PositionSizeType.FixedNotional,
        decimal? riskPerTradePercent = null)
    {
        return new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Test",
            Market = "BTC-USD",
            Grid = new GridConfig
            {
                Levels = gridLevels,
                Spacing = gridSpacing,
                BreakdownThreshold = breakdownThreshold,
                EntryMode = entryMode ?? EntryModes.AutoFromSignalCandle,
                AnchorPrice = manualAnchorPrice,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = takeProfitType, Value = takeProfitValue },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = stopLossPercent },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = positionSizeType,
                PositionSizeValue = positionSize,
                RiskPerTradePercent = riskPerTradePercent,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
    }

    private static StrategyConfig CreateSignalStrategyConfig(decimal rsiThreshold)
    {
        return new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = "RSI Signal Test",
            Market = "BTC",
            Timeframe = "15m",
            Direction = Direction.Long,
            EntryLogic = EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "rsi-entry",
                    Type = EntryConditionType.Rsi,
                    Enabled = true,
                    Params = new RsiParams { Period = 14, Operator = "lt", Value = rsiThreshold }
                }
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
            },
            Risk = new RiskConfig
            {
                PositionSizeValue = 1000m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
    }

    private static BacktestConfig CreateSignalBacktestConfig(StrategyConfig strategy)
    {
        return new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = (12 * OneHourMs) + (5 * FifteenMinutesMs),
            InitialCapital = 10_000m,
            Strategy = strategy,
            Execution = CreateExecutionConfig(),
            WarmupPeriod = 20,
            EnableAuditLog = true,
        };
    }

    private void SetupSignalModeCandles(BacktestConfig config)
    {
        var first15mTimestamp = config.StartDateUtc - (config.WarmupPeriod * FifteenMinutesMs);
        var closes = new[]
        {
            100m, 99m, 98m, 97m, 96m, 95m, 94m, 93m, 92m, 91m,
            90m, 89m, 88m, 87m, 86m, 85m, 84m, 83m, 82m, 81m,
            80m, 81m, 83m, 86m, 87m, 88m,
        };

        var candles15m = closes
            .Select((close, index) => CreateCandle(
                "15m",
                first15mTimestamp + (index * FifteenMinutesMs),
                close,
                close + 0.5m,
                Math.Max(0m, close - 0.5m),
                close))
            .ToList();

        SetupCandles("15m", candles15m);
        SetupCandles("1h",
        [
            CreateCandle("1h", 9 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 10 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 11 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 12 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("1h", 13 * OneHourMs, 100m, 101m, 99m, 100m),
        ]);
        SetupCandles("4h",
        [
            CreateCandle("4h", 4 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("4h", 8 * OneHourMs, 100m, 101m, 99m, 100m),
            CreateCandle("4h", 12 * OneHourMs, 100m, 101m, 99m, 100m),
        ]);
    }

    private static ExecutionConfig CreateExecutionConfig(
        decimal makerFeeRate = 0.0001m,
        decimal takerFeeRate = 0.00035m,
        decimal slippageRate = 0m)
    {
        return new ExecutionConfig
        {
            FeeModel = new FeeModel
            {
                MakerFeeRate = makerFeeRate,
                TakerFeeRate = takerFeeRate,
                SlippageRate = slippageRate,
            },
        };
    }
}