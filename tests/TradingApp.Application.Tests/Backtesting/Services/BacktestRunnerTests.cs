using System.Reflection;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestRunnerTests
{
    private const long FifteenMinutesMs = 15L * 60L * 1000L;
    private const long OneHourMs = 60L * 60L * 1000L;
    private const long FourHoursMs = 4L * 60L * 60L * 1000L;

    private Mock<ICandleRepository> _candleRepositoryMock = default!;
    private Mock<IMarketContextBuilder> _contextBuilderMock = default!;
    private Mock<IStrategyEngine> _strategyEngineMock = default!;
    private Mock<IGridController> _gridControllerMock = default!;
    private Mock<IRiskEngine> _riskEngineMock = default!;
    private Mock<IPositionManager> _positionManagerMock = default!;
    private Mock<ISignalController> _signalControllerMock = default!;
    private BacktestExecutionContextAccessor _executionContextAccessor = default!;
    private BacktestRunner _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _candleRepositoryMock = new Mock<ICandleRepository>();
        _contextBuilderMock = new Mock<IMarketContextBuilder>();
        _strategyEngineMock = new Mock<IStrategyEngine>();
        _gridControllerMock = new Mock<IGridController>();
        _riskEngineMock = new Mock<IRiskEngine>();
        _positionManagerMock = new Mock<IPositionManager>();
        _signalControllerMock = new Mock<ISignalController>();
        _executionContextAccessor = new BacktestExecutionContextAccessor();

        _sut = new BacktestRunner(
            _candleRepositoryMock.Object,
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            _executionContextAccessor,
            _signalControllerMock.Object);

        _contextBuilderMock
            .Setup(builder => builder.Build(It.IsAny<Candle>(), It.IsAny<Candle?>(), It.IsAny<Candle?>()))
            .Returns((Candle trigger, Candle? oneHour, Candle? fourHour) => new MarketContext
            {
                Symbol = trigger.Symbol,
                TimestampUtc = trigger.Timestamp,
                CurrentCandle = trigger,
                LatestOneHourCandle = oneHour,
                LatestFourHourCandle = fourHour,
                Indicators = new IndicatorSnapshot()
            });

        _contextBuilderMock
            .Setup(builder => builder.Build(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.IsAny<IReadOnlyList<IndicatorRequirement>?>()))
            .Returns((Candle trigger, Candle? oneHour, Candle? fourHour, IReadOnlyList<IndicatorRequirement>? _) => new MarketContext
            {
                Symbol = trigger.Symbol,
                TimestampUtc = trigger.Timestamp,
                CurrentCandle = trigger,
                LatestOneHourCandle = oneHour,
                LatestFourHourCandle = fourHour,
                Indicators = new IndicatorSnapshot()
            });

        _contextBuilderMock
            .Setup(builder => builder.BuildAsync(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.IsAny<IReadOnlyList<IndicatorRequirement>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Candle trigger, Candle? oneHour, Candle? fourHour, IReadOnlyList<IndicatorRequirement>? _, CancellationToken _ct) => new MarketContext
            {
                Symbol = trigger.Symbol,
                TimestampUtc = trigger.Timestamp,
                CurrentCandle = trigger,
                LatestOneHourCandle = oneHour,
                LatestFourHourCandle = fourHour,
                Indicators = new IndicatorSnapshot()
            });

        _strategyEngineMock
            .Setup(engine => engine.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<IStrategyConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategyEvaluation { SetupDetected = false });

        _gridControllerMock
            .Setup(controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());

        _riskEngineMock
            .Setup(engine => engine.ValidateAsync(It.IsAny<IReadOnlyList<TradingSignal>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());

        _signalControllerMock
            .Setup(controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());
    }

    [TestMethod]
    public async Task GivenValidConfig_WhenRunAsync_ThenReturnsBacktestResult()
    {
        var config = CreateConfig(warmupPeriod: 2);
        SetupCandles(config);

        var result = await _sut.RunAsync(config);

        result.Should().NotBeNull();
        result.TotalTrades.Should().Be(0);
        result.FinalEquity.Should().Be(config.InitialCapital);
        result.EquityTimeSeries.Should().HaveCount(4);
        result.TradeLog.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenInvalidDateRange_WhenRunAsync_ThenThrowsArgumentException()
    {
        var config = CreateConfig(startDateUtc: 2_000_000, endDateUtc: 1_000_000);

        var action = () => _sut.RunAsync(config);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Start date must be before end date.*");
    }

    [TestMethod]
    public async Task GivenZeroInitialCapital_WhenRunAsync_ThenThrowsArgumentException()
    {
        var config = CreateConfig(initialCapital: 0m);

        var action = () => _sut.RunAsync(config);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Initial capital must be greater than zero.*");
    }

    [TestMethod]
    public async Task GivenMissingRequiredInterval_WhenRunAsync_ThenThrowsArgumentException()
    {
        var config = CreateConfig(intervals: ["15m", "1h"]);

        var action = () => _sut.RunAsync(config);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*4h interval is required for strategy evaluation.*");
    }

    [TestMethod]
    public async Task GivenNullStrategyConfig_WhenRunAsync_ThenThrowsArgumentNullException()
    {
        var config = new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = 12 * OneHourMs,
            EndDateUtc = 13 * OneHourMs,
            InitialCapital = 10_000m,
            Strategy = null!,
            Execution = new ExecutionConfig
            {
                FeeModel = FeeModel.Default,
            },
        };

        var action = () => _sut.RunAsync(config);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task GivenMissingReplayData_WhenRunAsync_ThenPropagatesReplayError()
    {
        var config = CreateConfig();

        SetupCandles("15m", Array.Empty<Candle>());
        SetupCandles("1h", CreateCandles("1h", OneHourMs, 1));
        SetupCandles("4h", CreateCandles("4h", FourHoursMs, 1));

        var action = () => _sut.RunAsync(config);

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No candle data found for BTC/15m*");
    }

    [TestMethod]
    public async Task GivenSameInputs_WhenRunTwice_ThenResultsAreDeterministic()
    {
        var config = CreateConfig(warmupPeriod: 2);
        SetupCandles(config);

        var result1 = await _sut.RunAsync(config);
        var result2 = await _sut.RunAsync(config);

        result1.TotalTrades.Should().Be(result2.TotalTrades);
        result1.TotalPnL.Should().Be(result2.TotalPnL);
        result1.FinalEquity.Should().Be(result2.FinalEquity);
        result1.EquityTimeSeries.Should().BeEquivalentTo(result2.EquityTimeSeries, options => options.WithStrictOrdering());
        result1.TradeLog.Should().BeEquivalentTo(result2.TradeLog, options => options.WithStrictOrdering());
    }

    [TestMethod]
    public async Task GivenValidConfig_WhenRunAsync_ThenEquityTrackingStartsAtInitialCapital()
    {
        var config = CreateConfig(warmupPeriod: 2);
        SetupCandles(config);

        var result = await _sut.RunAsync(config);

        result.EquityTimeSeries.Should().NotBeEmpty();
        result.EquityTimeSeries[0].Equity.Should().Be(config.InitialCapital);
    }

    [TestMethod]
    public async Task GivenAuditLogDisabled_WhenRunCompletes_ThenAuditDataIsNull()
    {
        var config = CreateConfig(warmupPeriod: 2, enableAuditLog: false);
        SetupCandles(config);

        var result = await _sut.RunAsync(config);

        result.CandleEvaluationLog.Should().BeNull();
        result.OrderEventLog.Should().BeNull();
        result.GridCycleLog.Should().BeNull();
    }

    [TestMethod]
    public void GivenExitFillFromDifferentCycle_WhenRecordFill_ThenOnlyMatchingCycleTradeIsClosed()
    {
        var tradeLog = new List<BacktestTrade>();
        var gridState = new GridState { GridCycleId = "cycle-b", TotalLevels = 10 };

        InvokeRecordFill(tradeLog, gridState, CreateFill("entry-a", "cycle-a", OrderSide.Buy, TradeType.GridFill, 100m, 1m, 0.01m, 1_000));
        InvokeRecordFill(tradeLog, gridState, CreateFill("entry-b", "cycle-b", OrderSide.Buy, TradeType.GridFill, 101m, 1m, 0.01m, 2_000));

        InvokeRecordFill(tradeLog, gridState, CreateFill("tp-b", "cycle-b", OrderSide.Sell, TradeType.TakeProfit, 105m, 1m, 0.01m, 3_000));

        tradeLog.Should().ContainSingle(trade => trade.GridCycleId == "cycle-a" && trade.ExitTimeUtc == null);
        tradeLog.Should().ContainSingle(trade =>
            trade.GridCycleId == "cycle-b" &&
            trade.ExitTimeUtc == 3_000 &&
            trade.PnL == 4m);
    }

    [TestMethod]
    public void GivenExitFillClosesMultipleLevels_WhenRecordFill_ThenAllMatchingTradesReceivePnL()
    {
        var tradeLog = new List<BacktestTrade>();
        var gridState = new GridState { GridCycleId = "cycle-1", TotalLevels = 10 };

        InvokeRecordFill(tradeLog, gridState, CreateFill("entry-1", "cycle-1", OrderSide.Buy, TradeType.GridFill, 100m, 1m, 0.01m, 1_000));
        InvokeRecordFill(tradeLog, gridState, CreateFill("entry-2", "cycle-1", OrderSide.Buy, TradeType.GridFill, 98m, 2m, 0.02m, 2_000));

        InvokeRecordFill(tradeLog, gridState, CreateFill("tp-1", "cycle-1", OrderSide.Sell, TradeType.TakeProfit, 105m, 3m, 0.03m, 3_000));

        tradeLog.Should().HaveCount(2);
        tradeLog.Should().OnlyContain(trade => trade.ExitTimeUtc == 3_000);
        tradeLog.Sum(trade => trade.PnL).Should().Be(19m);
    }

    [TestMethod]
    public void GivenSignalEntryAndTakeProfitInSameCycle_WhenRecordFill_ThenSignalTradeIsClosed()
    {
        var tradeLog = new List<BacktestTrade>();
        var gridState = new GridState { TotalLevels = 10 };

        InvokeRecordFill(tradeLog, gridState, CreateFill("signal-entry", "signal", OrderSide.Buy, TradeType.SignalEntry, 81m, 1m, 0.01m, 1_000));
        InvokeRecordFill(tradeLog, gridState, CreateFill("signal-exit", "signal", OrderSide.Sell, TradeType.TakeProfit, 86m, 1m, 0.01m, 2_000));

        tradeLog.Should().ContainSingle(trade =>
            trade.TradeType == TradeType.SignalEntry &&
            trade.ExitTimeUtc == 2_000 &&
            trade.PnL == 5m);
    }

    private void SetupCandles(BacktestConfig config)
    {
        var first15mTimestamp = config.StartDateUtc - (config.WarmupPeriod * FifteenMinutesMs);
        var first1hTimestamp = OneHourMs;
        var first4hTimestamp = FourHoursMs;

        SetupCandles("15m", CreateCandles("15m", first15mTimestamp, 6));
        SetupCandles("1h", CreateCandles("1h", first1hTimestamp, 4));
        SetupCandles("4h", CreateCandles("4h", first4hTimestamp, 3));
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

    private static BacktestConfig CreateConfig(
        long startDateUtc = 12 * OneHourMs,
        long endDateUtc = 13 * OneHourMs,
        decimal initialCapital = 10_000m,
        int warmupPeriod = 2,
        IReadOnlyList<string>? intervals = null,
        IStrategyConfig? strategy = null,
        ExecutionConfig? execution = null,
        bool enableAuditLog = true)
    {
        return new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = intervals ?? ["15m", "1h", "4h"],
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            InitialCapital = initialCapital,
            Strategy = strategy ?? new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Grid,
                StrategyName = "Test",
                Market = "BTC-USD",
                Grid = new GridConfig
                {
                    Levels = 5,
                    Spacing = 0.5m,
                    BreakdownThreshold = 3m,
                },
                Exit = new ExitConfig
                {
                    TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                    StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
                },
                Risk = new RiskConfig
                {
                    PositionSizeValue = 100m,
                    Leverage = 1m,
                    MaxOpenTrades = 1,
                },
            },
            Execution = execution ?? new ExecutionConfig
            {
                FeeModel = FeeModel.Default,
            },
            WarmupPeriod = warmupPeriod,
            EnableAuditLog = enableAuditLog
        };
    }

    private static IReadOnlyList<Candle> CreateCandles(string interval, long startTimestamp, int count)
    {
        var intervalMs = interval switch
        {
            "15m" => FifteenMinutesMs,
            "1h" => OneHourMs,
            "4h" => FourHoursMs,
            _ => throw new ArgumentException($"Unsupported interval: {interval}", nameof(interval))
        };

        return Enumerable.Range(0, count)
            .Select(index => Candle.Create(
                "Binance",
                "BTC",
                interval,
                startTimestamp + (index * intervalMs),
                100m + index,
                101m + index,
                99m + index,
                100.5m + index,
                1_000m,
                10))
            .ToList();
    }

    private static void InvokeRecordFill(List<BacktestTrade> tradeLog, GridState gridState, SimulatedFill fill)
    {
        var method = typeof(BacktestRunner).GetMethod("RecordFill", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, [tradeLog, gridState, fill]);
    }

    private static SimulatedFill CreateFill(
        string orderId,
        string cycleId,
        OrderSide side,
        TradeType tradeType,
        decimal fillPrice,
        decimal size,
        decimal fee,
        long fillTimeUtc)
    {
        return new SimulatedFill
        {
            OrderId = orderId,
            FillTimeUtc = fillTimeUtc,
            FillPrice = fillPrice,
            Side = side,
            Size = size,
            Fee = fee,
            Symbol = "BTC",
            TradeType = tradeType,
            GridCycleId = cycleId,
            IsMaker = tradeType == TradeType.GridFill
        };
    }
}