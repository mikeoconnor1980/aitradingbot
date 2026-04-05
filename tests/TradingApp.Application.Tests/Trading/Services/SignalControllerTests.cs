using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Trading.Services;

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
            new StrategyEvaluation { SetupDetected = true, Reason = "RSI below 30." },
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
        signal.Parameters["notional"].Should().Be(1000m);
        signal.Parameters["orderType"].Should().Be(OrderType.Market.ToString());
    }

    [TestMethod]
    public async Task GivenNoSetupAndNoPosition_WhenProcessAsync_ThenEmitsNoSignals()
    {
        var signals = await _sut.ProcessAsync(
            new StrategyEvaluation { SetupDetected = false, Reason = "RSI above 30." },
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
            new StrategyEvaluation { SetupDetected = false },
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
            new StrategyEvaluation { SetupDetected = false },
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
            new StrategyEvaluation { SetupDetected = false },
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
            new StrategyEvaluation { SetupDetected = true },
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
            new StrategyEvaluation { SetupDetected = true, Reason = "RSI below 30." },
            CreateMarketContext(close: 50_000m, accountEquity: 10_000m),
            CreateGridState(),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        signals[0].Parameters!["notional"].Should().Be(500m);
        signals[0].Parameters["size"].Should().Be(0.01m);
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

    private static MarketContext CreateMarketContext(decimal close, decimal accountEquity = 0m)
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
            Indicators = new IndicatorSnapshot(),
            AccountEquity = accountEquity
        };
    }
}