using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class CompositeStrategyEngineTests
{
    private const long CandleTimestamp = 1_000_000;

    private Mock<IConditionEvaluator> _conditionEvaluatorMock = default!;
    private Mock<ITrendFilterEvaluator> _trendFilterEvaluatorMock = default!;
    private CompositeStrategyEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _conditionEvaluatorMock = new Mock<IConditionEvaluator>();
        _trendFilterEvaluatorMock = new Mock<ITrendFilterEvaluator>();
        _trendFilterEvaluatorMock
            .Setup(evaluator => evaluator.Evaluate(
                It.IsAny<TrendFilterConfig?>(),
                It.IsAny<Direction>(),
                It.IsAny<IndicatorContext>(),
                It.IsAny<MarketContext>()))
            .Returns(TrendFilterResult.Pass("Trend filter passed."));
        _sut = new CompositeStrategyEngine(
            new GridStrategyEngine(),
            _conditionEvaluatorMock.Object,
            _trendFilterEvaluatorMock.Object);
    }

    [TestMethod]
    public async Task GivenGridMode_WhenEvaluated_ThenDelegatesToGridEngine()
    {
        var config = CreateGridConfig();
        var context = CreateMarketContext(includeHigherTimeframes: true);

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.Reason.Should().StartWith("Grid setup available.");
        _conditionEvaluatorMock.Verify(
            evaluator => evaluator.Evaluate(It.IsAny<StrategyConfig>(), It.IsAny<MarketContext>()),
            Times.Never);
        _trendFilterEvaluatorMock.Verify(
            evaluator => evaluator.Evaluate(
                It.IsAny<TrendFilterConfig?>(),
                It.IsAny<Direction>(),
                It.IsAny<IndicatorContext>(),
                It.IsAny<MarketContext>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenSignalMode_WhenConditionsPass_ThenMapsConditionEvaluationResult()
    {
        var config = CreateSignalConfig();
        var context = CreateMarketContext(includeHigherTimeframes: true);
        _conditionEvaluatorMock
            .Setup(evaluator => evaluator.Evaluate(config, context))
            .Returns(new ConditionEvaluationResult
            {
                SetupDetected = true,
                ConditionResults = [],
                OverallReason = "All 1 conditions passed."
            });

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeTrue();
        result.TrendFilterPassed.Should().BeTrue();
        result.Reason.Should().Be("All 1 conditions passed.");
        _conditionEvaluatorMock.Verify(evaluator => evaluator.Evaluate(config, context), Times.Once);
    }

    [TestMethod]
    public async Task GivenSignalMode_WhenConditionsFail_ThenReturnsNoSetup()
    {
        var config = CreateSignalConfig();
        var context = CreateMarketContext(includeHigherTimeframes: false);
        _conditionEvaluatorMock
            .Setup(evaluator => evaluator.Evaluate(config, context))
            .Returns(new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "1/1 conditions failed."
            });

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeFalse();
        result.TrendFilterPassed.Should().BeTrue();
        result.Reason.Should().Be("1/1 conditions failed.");
        _conditionEvaluatorMock.Verify(evaluator => evaluator.Evaluate(config, context), Times.Once);
    }

    [TestMethod]
    public async Task GivenSignalMode_WhenTrendFilterFails_ThenNoSetupAndConditionsNotEvaluated()
    {
        var config = CreateSignalConfig();
        var context = CreateMarketContext(includeHigherTimeframes: true);
        _trendFilterEvaluatorMock
            .Setup(evaluator => evaluator.Evaluate(config.TrendFilter, config.Direction, context.IndicatorContext!, context))
            .Returns(TrendFilterResult.Fail("EMA(50) < EMA(200)"));

        var result = await _sut.EvaluateAsync(context, config);

        result.SetupDetected.Should().BeFalse();
        result.TrendFilterPassed.Should().BeFalse();
        result.Reason.Should().Contain("Trend filter failed");
        _conditionEvaluatorMock.Verify(
            evaluator => evaluator.Evaluate(It.IsAny<StrategyConfig>(), It.IsAny<MarketContext>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenNonStrategyConfig_WhenEvaluated_ThenThrowsArgumentException()
    {
        var context = CreateMarketContext(includeHigherTimeframes: true);
        var strategyConfig = new TestStrategyConfig();

        var act = () => _sut.EvaluateAsync(context, strategyConfig);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Expected StrategyConfig but received TestStrategyConfig.*");
    }

    private static StrategyConfig CreateGridConfig()
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            StrategyName = "Grid Strategy",
            Market = "BTC-USD",
            Grid = new GridConfig
            {
                Levels = 5,
                Spacing = 0.5m,
            },
            Risk = new RiskConfig
            {
                PositionSizeValue = 100m,
            }
        };
    }

    private static StrategyConfig CreateSignalConfig()
    {
        return new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            StrategyName = "Signal Strategy",
            Market = "BTC-USD",
            Direction = Direction.Long,
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.EmaCross,
                FastPeriod = 50,
                SlowPeriod = 200,
                Operator = TrendOperator.Gt,
                AppliesTo = Direction.Long,
            },
            EntryLogic = EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = "rsi-1",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI(14)",
                    Params = new RsiParams
                    {
                        Period = 14,
                        Operator = "lt",
                        Value = 40m
                    }
                }
            ],
            Risk = new RiskConfig
            {
                PositionSizeValue = 100m,
            }
        };
    }

    private static MarketContext CreateMarketContext(bool includeHigherTimeframes)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = CreateCandle("15m", CandleTimestamp),
            PreviousCandle = CreateCandle("15m", CandleTimestamp - 60_000L),
            LatestOneHourCandle = includeHigherTimeframes ? CreateCandle("1h", CandleTimestamp) : null,
            LatestFourHourCandle = includeHigherTimeframes ? CreateCandle("4h", CandleTimestamp) : null,
            Indicators = new IndicatorSnapshot(),
            IndicatorContext = new IndicatorContext()
        };
    }

    private static Candle CreateCandle(string interval, long timestamp)
    {
        return Candle.Create(
            "Binance",
            "BTC-USD",
            interval,
            timestamp,
            100m,
            105m,
            95m,
            102m,
            1_000m,
            10);
    }

    private sealed class TestStrategyConfig : IStrategyConfig
    {
    }
}