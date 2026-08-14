using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class SignalControllerTests
{
    private const long CandleTimestamp = 1_000_000;

    private static readonly StrategyConfig DefaultConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Signal,
        StrategyName = "Test Signal",
        Market = "BTC-USD",
        EntryLogic = EntryLogic.All,
        EntryConditions =
        [
            new EntryConditionConfig
            {
                Id = "rsi-entry",
                Type = EntryConditionType.Rsi,
                Enabled = true,
                Params = new RsiParams { Period = 14, Operator = "lt", Value = 30m }
            }
        ],
        Exit = new ExitConfig
        {
            TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
            StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m }
        },
        Risk = new RiskConfig
        {
            PositionSizeType = PositionSizeType.FixedNotional,
            PositionSizeValue = 1000m,
            Leverage = 1m,
            MaxOpenTrades = 1
        }
    };

    private SignalController _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new SignalController();
    }

    [TestMethod]
    public async Task GivenSetupDetectedAndNoPosition_WhenProcessAsync_ThenEmitsOpenPosition()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("OpenPosition");
        signal.Symbol.Should().Be("BTC-USD");
        signal.Reason.Should().Be("RSI below 30.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["entryPrice"].Should().Be(50_000m);
        signal.Parameters["size"].Should().Be(0.02m);
        signal.Parameters["notionalUsd"].Should().Be(1000m);
        signal.Parameters["orderType"].Should().Be(OrderType.Market.ToString());
    }

    [TestMethod]
    public async Task GivenNoSetupAndNoPosition_WhenProcessAsync_ThenEmitsNoSignals()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false, Reason = "RSI above 30." },
            CreateMarketContext(close: 50_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenOpenPositionAndStopLossTriggered_WhenProcessAsync_ThenEmitsTakeProfitWithStopLoss()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false },
            CreateMarketContext(close: 47_000m),
            CreateGridState(),
            CreatePositionState(size: 0.02m, averageEntryPrice: 50_000m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Stop loss triggered.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["targetPrice"].Should().Be(47_000m);
        signal.Parameters["size"].Should().Be(0.02m);
        signal.Parameters["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["cancellationReason"].Should().Be(CancellationReason.StopLossTriggered.ToString());
    }

    [TestMethod]
    public async Task GivenOpenPositionAndTakeProfitTriggered_WhenProcessAsync_ThenEmitsTakeProfitSignal()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false },
            CreateMarketContext(close: 51_500m),
            CreateGridState(),
            CreatePositionState(size: 0.02m, averageEntryPrice: 50_000m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Take profit triggered.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["targetPrice"].Should().Be(51_500m);
        signal.Parameters["size"].Should().Be(0.02m);
        signal.Parameters["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["cancellationReason"].Should().Be(CancellationReason.TakeProfitTriggered.ToString());
    }

    [TestMethod]
    public async Task GivenOpenPositionWithinBands_WhenProcessAsync_ThenEmitsNoSignals()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false },
            CreateMarketContext(close: 50_500m),
            CreateGridState(),
            CreatePositionState(size: 0.02m, averageEntryPrice: 50_000m),
            DefaultConfig);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenZeroEntryPrice_WhenProcessAsync_ThenDoesNotEmitOpenPosition()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true },
            CreateMarketContext(close: 0m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenPercentWalletSizing_WhenProcessAsync_ThenUsesAccountEquityToResolveNotional()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m
            }
        };

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, accountEquity: 10_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        var parameters = signals[0].Parameters;
        parameters.Should().NotBeNull();
        parameters!["notionalUsd"].Should().Be(500m);
        parameters["size"].Should().Be(0.01m);
    }

    [TestMethod]
    public async Task GivenDrawdownScalingFactor_WhenProcessAsync_ThenResolvedNotionalIsScaled()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, drawdownScalingFactor: 0.5m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var parameters = signals[0].Parameters;
        parameters.Should().NotBeNull();
        parameters!["notionalUsd"].Should().Be(500m);
        parameters["size"].Should().Be(0.01m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithFixedPercentStopLoss_WhenProcessAsync_ThenUsesRBasedNotional()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 2m,
                },
            },
        };

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, accountEquity: 10_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        var parameters = signals[0].Parameters;
        parameters.Should().NotBeNull();
        parameters!["notionalUsd"].Should().Be(5_000m);
        parameters["size"].Should().Be(0.1m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithNoStopLoss_WhenProcessAsync_ThenEmitsNoSignals()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = false,
                },
            },
        };

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, accountEquity: 10_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLoss_WhenOpenPositionEmitted_ThenCapturesAtrAtEntry()
    {
        var config = DefaultConfig with
        {
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2m,
                },
            },
        };
        var gridState = CreateGridState();

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, atr: 500m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("OpenPosition");
        gridState.AtrAtEntry.Should().Be(500m);
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLoss_WhenPriceBreachesLockedStop_ThenEmitsStopLossAndClearsAtrAtEntry()
    {
        var config = DefaultConfig with
        {
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2m,
                    Value = 1m,
                },
            },
        };
        var gridState = CreateGridState();
        gridState.AtrAtEntry = 500m;

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false },
            CreateMarketContext(close: 48_900m, atr: 800m),
            gridState,
            CreatePositionState(size: 0.02m, averageEntryPrice: 50_000m),
            config);

        signals.Should().ContainSingle();
        signals[0].Reason.Should().Be("ATR initial stop triggered (stop: 49000.00).");
        signals[0].Parameters!["cancellationReason"].Should().Be(CancellationReason.StopLossTriggered.ToString());
        gridState.AtrAtEntry.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLossWithFallbackValue_WhenPriceBreachesFallbackButNotLockedStop_ThenDoesNotUseFixedStopLossGuard()
    {
        var config = DefaultConfig with
        {
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2m,
                    Value = 1m,
                },
            },
        };
        var gridState = CreateGridState();
        gridState.AtrAtEntry = 500m;

        var signals = await _sut.ProcessAsync(
            new StrategyEvaluationResult { SetupDetected = false },
            CreateMarketContext(close: 49_400m, atr: 800m),
            gridState,
            CreatePositionState(size: 0.02m, averageEntryPrice: 50_000m),
            config);

        signals.Should().BeEmpty();
        gridState.AtrAtEntry.Should().Be(500m);
    }

    private static PositionState CreatePositionState(decimal size, decimal averageEntryPrice)
    {
        return new PositionState
        {
            Symbol = "BTC-USD",
            Size = size,
            AverageEntryPrice = averageEntryPrice,
            UnrealisedPnL = 0m
        };
    }

    private static GridState CreateGridState()
    {
        return new GridState
        {
            Lifecycle = GridLifecycle.Inactive,
            FilledLevels = 0,
            TotalLevels = 0,
        };
    }

    private static MarketContext CreateMarketContext(
        decimal close,
        decimal atr = 0m,
        decimal accountEquity = 0m,
        decimal drawdownScalingFactor = 1.0m)
    {
        return new MarketContext
        {
            Symbol = "BTC-USD",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance",
                "BTC-USD",
                "15m",
                CandleTimestamp,
                close,
                close + 100m,
                Math.Max(0m, close - 100m),
                close,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot
            {
                Atr = atr,
            },
            AccountEquity = accountEquity,
            DrawdownScalingFactor = drawdownScalingFactor
        };
    }
}