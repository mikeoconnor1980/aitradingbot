using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Scheduling;
using TradingApp.Application.Scheduling.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Scheduling;

[TestClass]
public sealed class StrategySchedulerTests
{
    private static readonly StrategyConfig TestConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Grid,
        StrategyName = "Test Grid",
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
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 100m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };

    private static readonly StrategyConfig SignalTestConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test RSI Signal",
        Market = "BTC-USD",
        EntryLogic = EntryLogic.All,
        EntryConditions =
        [
            new EntryConditionConfig
            {
                Id = "rsi-entry",
                Enabled = true,
                Type = EntryConditionType.Rsi,
                Label = "RSI(14) < 30",
                Params = new RsiParams
                {
                    Period = 14,
                    Operator = "lt",
                    Value = 30m
                }
            }
        ],
        Exit = new ExitConfig
        {
            TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
            StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m },
        },
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 100m,
            Leverage = 1m,
            MaxOpenTrades = 1,
        },
    };

    private Mock<IMarketContextBuilder> _contextBuilderMock = default!;
    private Mock<IStrategyEngine> _strategyEngineMock = default!;
    private Mock<IGridController> _gridControllerMock = default!;
    private Mock<ISignalController> _signalControllerMock = default!;
    private Mock<IRiskEngine> _riskEngineMock = default!;
    private Mock<IPositionManager> _positionManagerMock = default!;
    private StrategyScheduler _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _contextBuilderMock = new Mock<IMarketContextBuilder>();
        _strategyEngineMock = new Mock<IStrategyEngine>();
        _gridControllerMock = new Mock<IGridController>();
        _signalControllerMock = new Mock<ISignalController>();
        _riskEngineMock = new Mock<IRiskEngine>();
        _positionManagerMock = new Mock<IPositionManager>();

        _sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            TestConfig,
            signalController: _signalControllerMock.Object);

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
            .ReturnsAsync(new StrategyEvaluation { SetupDetected = true });

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
    public async Task GivenNon15mEvent_WhenHandleCandleClosedAsync_ThenPipelineNotCalled()
    {
        var evt = CreateEvent("1h");

        await _sut.HandleCandleClosedAsync(evt, null, null);

        _contextBuilderMock.Verify(builder => builder.BuildAsync(
            It.IsAny<Candle>(),
            It.IsAny<Candle?>(),
            It.IsAny<Candle?>(),
            It.IsAny<IReadOnlyList<IndicatorRequirement>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _strategyEngineMock.Verify(engine => engine.EvaluateAsync(It.IsAny<MarketContext>(), It.IsAny<IStrategyConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _gridControllerMock.Verify(controller => controller.ProcessAsync(
            It.IsAny<StrategyEvaluation>(),
            It.IsAny<MarketContext>(),
            It.IsAny<GridState>(),
            It.IsAny<PositionState>(),
            It.IsAny<IStrategyConfig>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Given15mEvent_WhenHandleCandleClosedAsync_ThenPipelineRunsInOrder()
    {
        var callOrder = new List<string>();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var marketContext = new MarketContext
        {
            Symbol = "BTC",
            TimestampUtc = 1_000,
            CurrentCandle = CreateCandle("15m"),
            LatestOneHourCandle = null,
            LatestFourHourCandle = null,
            Indicators = new IndicatorSnapshot()
        };
        var evaluation = new StrategyEvaluation { SetupDetected = true };
        IReadOnlyList<TradingSignal> signals =
        [
            new TradingSignal
            {
                SignalType = "deploy-grid",
                Symbol = "BTC"
            }
        ];

        _contextBuilderMock
            .Setup(builder => builder.BuildAsync(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.IsAny<IReadOnlyList<IndicatorRequirement>?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("context"))
            .ReturnsAsync(marketContext);

        _strategyEngineMock
            .Setup(engine => engine.EvaluateAsync(marketContext, TestConfig, cancellationToken))
            .Callback(() => callOrder.Add("strategy"))
            .ReturnsAsync(evaluation);

        _gridControllerMock
            .Setup(controller => controller.ProcessAsync(
                evaluation,
                marketContext,
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                TestConfig,
                cancellationToken))
            .Callback(() => callOrder.Add("grid"))
            .ReturnsAsync(signals);

        _riskEngineMock
            .Setup(engine => engine.ValidateAsync(signals, cancellationToken))
            .Callback(() => callOrder.Add("risk"))
            .ReturnsAsync(signals);

        _positionManagerMock
            .Setup(manager => manager.ExecuteSignalsAsync(signals, cancellationToken))
            .Callback(() => callOrder.Add("position"))
            .Returns(Task.CompletedTask);

        await _sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null, cancellationToken);

        callOrder.Should().Equal("context", "strategy", "grid", "risk", "position");
    }

    [TestMethod]
    public async Task GivenSignalModeConfig_WhenHandleCandleClosedAsync_ThenSignalControllerCalledNotGridController()
    {
        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            SignalTestConfig,
            signalController: _signalControllerMock.Object);

        await sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _signalControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _gridControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenGridModeConfig_WhenHandleCandleClosedAsync_ThenGridControllerCalledNotSignalController()
    {
        await _sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _gridControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _signalControllerMock.Verify(
            controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenSignalModeConfig_WhenHandleCandleClosedAsync_ThenFourArgBuildCalledWithIndicatorRequirements()
    {
        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            SignalTestConfig);

        await sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _contextBuilderMock.Verify(
            builder => builder.BuildAsync(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.Is<IReadOnlyList<IndicatorRequirement>?>(requirements =>
                    requirements != null &&
                    requirements.Count == 1 &&
                    requirements[0].Type == "RSI" &&
                    requirements[0].Period == 14),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenGridModeConfig_WhenHandleCandleClosedAsync_ThenFourArgBuildCalledWithNullRequirements()
    {
        await _sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _contextBuilderMock.Verify(
            builder => builder.BuildAsync(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.Is<IReadOnlyList<IndicatorRequirement>?>(requirements => requirements == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenSignalModeWithRsi_WhenHandleCandleClosedAsync_ThenIndicatorContextPopulated()
    {
        var indicatorContext = new IndicatorContext();
        indicatorContext.SetRsi(14, 25m);

        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            SignalTestConfig);

        _contextBuilderMock
            .Setup(builder => builder.BuildAsync(
                It.IsAny<Candle>(),
                It.IsAny<Candle?>(),
                It.IsAny<Candle?>(),
                It.Is<IReadOnlyList<IndicatorRequirement>?>(requirements =>
                    requirements != null &&
                    requirements.Count == 1 &&
                    requirements[0].Type == "RSI" &&
                    requirements[0].Period == 14),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Candle trigger, Candle? oneHour, Candle? fourHour, IReadOnlyList<IndicatorRequirement>? _, CancellationToken _ct) => new MarketContext
            {
                Symbol = trigger.Symbol,
                TimestampUtc = trigger.Timestamp,
                CurrentCandle = trigger,
                LatestOneHourCandle = oneHour,
                LatestFourHourCandle = fourHour,
                Indicators = new IndicatorSnapshot(),
                IndicatorContext = indicatorContext
            });

        await sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _strategyEngineMock.Verify(
            engine => engine.EvaluateAsync(
                It.Is<MarketContext>(context =>
                    context.IndicatorContext != null &&
                    context.IndicatorContext.GetRsi(14).HasValue &&
                    context.IndicatorContext.GetRsi(14)!.Value == 25m),
                SignalTestConfig,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoSignals_WhenHandleCandleClosedAsync_ThenPositionManagerNotCalled()
    {
        _gridControllerMock
            .Setup(controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());

        await _sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _riskEngineMock.Verify(engine => engine.ValidateAsync(It.IsAny<IReadOnlyList<TradingSignal>>(), It.IsAny<CancellationToken>()), Times.Never);
        _positionManagerMock.Verify(manager => manager.ExecuteSignalsAsync(It.IsAny<IReadOnlyList<TradingSignal>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenNoApprovedSignals_WhenHandleCandleClosedAsync_ThenPositionManagerNotCalled()
    {
        IReadOnlyList<TradingSignal> signals =
        [
            new TradingSignal
            {
                SignalType = "deploy-grid",
                Symbol = "BTC"
            }
        ];

        _gridControllerMock
            .Setup(controller => controller.ProcessAsync(
                It.IsAny<StrategyEvaluation>(),
                It.IsAny<MarketContext>(),
                It.IsAny<GridState>(),
                It.IsAny<PositionState>(),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(signals);

        _riskEngineMock
            .Setup(engine => engine.ValidateAsync(signals, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TradingSignal>());

        await _sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _positionManagerMock.Verify(manager => manager.ExecuteSignalsAsync(It.IsAny<IReadOnlyList<TradingSignal>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenBacktestExecutionContext_WhenHandleCandleClosedAsync_ThenContextIncludesCurrentAccountEquity()
    {
        var accessor = new BacktestExecutionContextAccessor();
        var executionEngine = new SimulatedExecutionEngine(new FeeModel());
        accessor.CurrentExecutionEngine = executionEngine;
        executionEngine.GetPosition().RealisedPnL = 125m;
        executionEngine.GetPosition().UnrealisedPnL = -25m;

        var sut = new StrategyScheduler(
            _contextBuilderMock.Object,
            _strategyEngineMock.Object,
            _gridControllerMock.Object,
            _riskEngineMock.Object,
            _positionManagerMock.Object,
            TestConfig,
            signalController: _signalControllerMock.Object,
            initialCapital: 10_000m,
            executionContextAccessor: accessor);

        await sut.HandleCandleClosedAsync(CreateEvent("15m"), null, null);

        _strategyEngineMock.Verify(
            engine => engine.EvaluateAsync(
                It.Is<MarketContext>(context => context.AccountEquity == 10_100m),
                It.IsAny<IStrategyConfig>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CandleClosedEvent CreateEvent(string timeframe)
    {
        return new CandleClosedEvent
        {
            Symbol = "BTC",
            Timeframe = timeframe,
            OpenTimeUtc = 1_000,
            CloseTimeUtc = 1_000 + (15L * 60L * 1000L),
            Candle = CreateCandle(timeframe)
        };
    }

    private static Candle CreateCandle(string interval)
    {
        return Candle.Create(
            "Binance",
            "BTC",
            interval,
            1_000,
            100m,
            105m,
            95m,
            102m,
            1_000m,
            10);
    }
}