using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class GridControllerTests
{
    private const long CandleTimestamp = 1_000_000;
    private static readonly StrategyConfig DefaultConfig = new()
    {
        SchemaVersion = 1,
        StrategyMode = StrategyMode.Grid,
        StrategyName = "Test Grid",
        Market = "BTC-USD",
        Grid = new GridConfig
        {
            Levels = 5,
            Spacing = 0.5m,
            BreakdownThreshold = 2m,
        },
        Exit = new ExitConfig
        {
            TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 1m },
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

    private GridController _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new GridController();
    }

    [TestMethod]
    public async Task GivenNoSetupAndNoPosition_WhenProcessAsync_ThenReturnsEmptySignals()
    {
        var signals = await _sut.ProcessAsync(
            CreateEvaluation(setupDetected: false),
            CreateMarketContext(close: 100m),
            CreateGridState(GridLifecycle.Inactive),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenInactiveGrid_WhenSetupDetectedAndNoPosition_ThenEmitsDeployGrid()
    {
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().ContainSingle();
        signals[0].SignalType.Should().Be("DeployGrid");
        gridState.Lifecycle.Should().Be(GridLifecycle.Deploying);
        gridState.TotalLevels.Should().Be(5);
        gridState.FilledLevels.Should().Be(0);
        gridState.GridCycleId.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGridAndNoPosition_WhenSetupDetected_ThenDoesNotRedeploy()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenPositionOpenAndNoExitCondition_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 99m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 99.5m),
            DefaultConfig);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenCandleCloseReachesTakeProfit_ThenEmitsMarketExitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 101m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 99.5m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Take profit triggered (partial fill).");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["targetPrice"].Should().Be(101m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenPartiallyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketExitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 100m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Reason.Should().Be("Stop loss triggered.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signal.Parameters["targetPrice"].Should().Be(94m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenPositionOpen_ThenEmitsLimitTakeProfitAndClosing()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 99.5m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 99m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.SignalType.Should().Be("TakeProfit");
        signal.Reason.Should().Be("Take profit active.");
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        signal.Parameters["targetPrice"].Should().Be(99.99m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenFullyFilledGrid_WhenStopLossTriggered_ThenEmitsMarketExitInsteadOfLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 100m),
            DefaultConfig);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signals[0].Reason.Should().Be("Stop loss triggered.");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenPositionStillOpenAndNoStopLoss_ThenReturnsEmptySignals()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 99m),
            DefaultConfig);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenClosingLifecycle_WhenStopLossTriggered_ThenEmitsMarketExit()
    {
        var gridState = CreateGridState(GridLifecycle.Closing, filledLevels: 5);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 94m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 100m),
            DefaultConfig);

        signals.Should().ContainSingle();
        signals[0].Parameters!["orderType"].Should().Be(OrderType.Market.ToString());
        signals[0].Reason.Should().Be("Stop loss triggered.");
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenActiveLifecycle_WhenPositionOpen_ThenUsesFallbackLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.Active, filledLevels: 1);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 1m, averageEntryPrice: 99m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        signal.Parameters["targetPrice"].Should().Be(99.99m);
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenDeployingLifecycle_WhenPositionOpen_ThenUsesFallbackLimitTakeProfit()
    {
        var gridState = CreateGridState(GridLifecycle.Deploying, filledLevels: 1);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m),
            gridState,
            CreatePositionState(size: 1m, averageEntryPrice: 99m),
            DefaultConfig);

        signals.Should().ContainSingle();
        var signal = signals[0];
        signal.Parameters.Should().NotBeNull();
        signal.Parameters!["orderType"].Should().Be(OrderType.Limit.ToString());
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
    }

    [TestMethod]
    public async Task GivenPercentWalletSizing_WhenDeployingGrid_ThenUsesAccountEquityToResolveNotional()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m
            }
        };
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        signals[0].Parameters!["notionalUsd"].Should().Be(500m);
    }

    [TestMethod]
    public async Task GivenDrawdownScalingFactor_WhenDeployingGrid_ThenResolvedNotionalIsScaled()
    {
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, drawdownScalingFactor: 0.5m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            DefaultConfig);

        signals.Should().ContainSingle();
        signals[0].Parameters!["notionalUsd"].Should().Be(50m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithTenLevelsAndFixedPercentStopLoss_WhenDeployingGrid_ThenDividesNotionalByLevels()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Grid = DefaultConfig.Grid! with
            {
                Levels = 10,
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
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        signals[0].Parameters!["notionalUsd"].Should().Be(500m);
        gridState.InitialRDollars.Should().Be(100m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithAtrInitialStopLoss_WhenAtrDoubles_ThenNotionalPerLevelHalves()
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
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2m,
                    Value = 3m,
                },
            },
        };

        var lowVolGridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);
        var highVolGridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var lowVolSignals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 50_000m, atr: 500m, accountEquity: 10_000m),
            lowVolGridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        var highVolSignals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 50_000m, atr: 1_000m, accountEquity: 10_000m),
            highVolGridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        lowVolSignals.Should().ContainSingle();
        highVolSignals.Should().ContainSingle();
        lowVolSignals[0].Parameters!["notionalUsd"].Should().Be(1_000m);
        highVolSignals[0].Parameters!["notionalUsd"].Should().Be(500m);
        lowVolGridState.AtrAtEntry.Should().Be(500m);
        highVolGridState.AtrAtEntry.Should().Be(1_000m);
    }

    [TestMethod]
    public async Task GivenTrackedRiskBasedGrid_WhenStopLossTriggered_ThenInitialRIsCleared()
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
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5);
        gridState.InitialRDollars = 100m;

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 97m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 100m),
            config);

        signals.Should().ContainSingle();
        gridState.InitialRDollars.Should().BeNull();
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
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5, atrAtEntry: 500m);
        gridState.InitialRDollars = 100m;

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 48_900m, atr: 800m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 50_000m),
            config);

        signals.Should().ContainSingle();
        signals[0].Reason.Should().Be("ATR initial stop triggered (stop: 49000.00).");
        signals[0].Parameters!["cancellationReason"].Should().Be(CancellationReason.StopLossTriggered.ToString());
        gridState.Lifecycle.Should().Be(GridLifecycle.Closing);
        gridState.InitialRDollars.Should().BeNull();
        gridState.AtrAtEntry.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLossWithFallbackValue_WhenPriceBreachesFallbackButNotLockedStop_ThenDoesNotTriggerFixedStop()
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
        var gridState = CreateGridState(GridLifecycle.FullyFilled, filledLevels: 5, atrAtEntry: 500m);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 49_400m, atr: 800m),
            gridState,
            CreatePositionState(size: 5m, averageEntryPrice: 50_000m),
            config);

        signals.Should().ContainSingle();
        signals[0].Reason.Should().Be("Take profit active.");
        gridState.AtrAtEntry.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLoss_WhenLiveAtrChanges_ThenLockedAtrStillControlsExit()
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
        var gridState = CreateGridState(GridLifecycle.PartiallyFilled, filledLevels: 2, atrAtEntry: 500m);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 49_500m, atr: 800m),
            gridState,
            CreatePositionState(size: 2m, averageEntryPrice: 50_000m),
            config);

        signals.Should().BeEmpty();
        gridState.Lifecycle.Should().Be(GridLifecycle.PartiallyFilled);
        gridState.AtrAtEntry.Should().Be(500m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithGridBreakdownFallback_WhenDeployingGrid_ThenUsesBreakdownThreshold()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Grid = DefaultConfig.Grid! with
            {
                BreakdownThreshold = 5m,
            },
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = false,
                },
            },
        };
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().ContainSingle();
        signals[0].Parameters!["notionalUsd"].Should().Be(400m);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizingWithNoResolvableStopLoss_WhenDeployingGrid_ThenEmitsNoSignals()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Grid = DefaultConfig.Grid! with
            {
                BreakdownThreshold = 0m,
            },
            Exit = DefaultConfig.Exit with
            {
                StopLoss = DefaultConfig.Exit.StopLoss with
                {
                    Enabled = false,
                },
            },
        };
        var gridState = CreateGridState(GridLifecycle.Inactive, totalLevels: 0);

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            gridState,
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        signals.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenAutoLeverageEnabledWithRiskBased_WhenDeployGridEmitted_ThenComputesLeverageFromMarketContext()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
                AutoLeverage = true,
                Leverage = 10m,
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
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m, maxLeverage: 50),
            CreateGridState(GridLifecycle.Inactive, totalLevels: 0),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        var deployGrid = signals.Should().ContainSingle().Subject;
        deployGrid.SignalType.Should().Be("DeployGrid");
        deployGrid.Parameters!["leverage"].Should().Be(33);
        deployGrid.Parameters["isIsolated"].Should().Be(true);
    }

    [TestMethod]
    public async Task GivenAutoLeverageDisabled_WhenDeployGridEmitted_ThenUsesManualLeverage()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                AutoLeverage = false,
                Leverage = 10m,
            },
        };

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            CreateGridState(GridLifecycle.Inactive, totalLevels: 0),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        var deployGrid = signals.Should().ContainSingle().Subject;
        deployGrid.Parameters!["leverage"].Should().Be(10);
        deployGrid.Parameters["isIsolated"].Should().Be(false);
    }

    [TestMethod]
    public async Task GivenPercentWalletSizingWithAutoLeverageEnabled_WhenDeployGridEmitted_ThenIgnoresAutoLeverageAndUsesManualValue()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m,
                AutoLeverage = true,
                Leverage = 7m,
            },
        };

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m, maxLeverage: 50),
            CreateGridState(GridLifecycle.Inactive, totalLevels: 0),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        var deployGrid = signals.Should().ContainSingle().Subject;
        deployGrid.Parameters!["leverage"].Should().Be(7);
        deployGrid.Parameters["isIsolated"].Should().Be(false);
    }

    [TestMethod]
    public async Task GivenRiskBasedSizing_WhenDeployGridEmitted_ThenIsolatedMarginIsAlwaysTrue()
    {
        var config = DefaultConfig with
        {
            Risk = DefaultConfig.Risk with
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
                AutoLeverage = false,
                Leverage = 3m,
            },
        };

        var signals = await _sut.ProcessAsync(
            CreateEvaluation(),
            CreateMarketContext(close: 100m, accountEquity: 10_000m),
            CreateGridState(GridLifecycle.Inactive, totalLevels: 0),
            CreatePositionState(size: 0m, averageEntryPrice: 0m),
            config);

        var deployGrid = signals.Should().ContainSingle().Subject;
        deployGrid.Parameters!["isIsolated"].Should().Be(true);
        deployGrid.Parameters["leverage"].Should().Be(3);
    }

    private static StrategyEvaluation CreateEvaluation(bool setupDetected = true)
    {
        return new StrategyEvaluation
        {
            SetupDetected = setupDetected,
            Reason = "Test setup"
        };
    }

    private static GridState CreateGridState(
        GridLifecycle lifecycle,
        int filledLevels = 0,
        int totalLevels = 5,
        string? gridCycleId = "test-cycle-001",
        decimal? atrAtEntry = null)
    {
        return new GridState
        {
            Lifecycle = lifecycle,
            GridCycleId = gridCycleId,
            FilledLevels = filledLevels,
            TotalLevels = totalLevels,
            AtrAtEntry = atrAtEntry,
        };
    }

    private static PositionState CreatePositionState(decimal size, decimal averageEntryPrice)
    {
        return new PositionState
        {
            Symbol = "BTC",
            Size = size,
            AverageEntryPrice = averageEntryPrice,
            UnrealisedPnL = 0m
        };
    }

    private static MarketContext CreateMarketContext(
        decimal close,
        decimal atr = 0m,
        decimal accountEquity = 0m,
        int? maxLeverage = null,
        decimal drawdownScalingFactor = 1.0m)
    {
        return new MarketContext
        {
            Symbol = "BTC",
            TimestampUtc = CandleTimestamp,
            CurrentCandle = Candle.Create(
                "Binance",
                "BTC",
                "15m",
                CandleTimestamp,
                close,
                close + 1m,
                close - 1m,
                close,
                1_000m,
                10),
            Indicators = new IndicatorSnapshot
            {
                Atr = atr,
            },
            AccountEquity = accountEquity,
            DrawdownScalingFactor = drawdownScalingFactor,
            MaxLeverage = maxLeverage
        };
    }
}