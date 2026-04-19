using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Enums;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class LivePositionManagerTests
{
    private Mock<IExecutionEngine> _executionEngine = null!;
    private Mock<IRiskEngine> _riskEngine = null!;
    private LivePositionManager _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _executionEngine = new Mock<IExecutionEngine>();
        _executionEngine.Setup(e => e.PlaceOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("order-1");
        _executionEngine.Setup(e => e.CancelAllOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _riskEngine = new Mock<IRiskEngine>();

        _sut = new LivePositionManager(
            _executionEngine.Object,
            new InMemoryOrderTracker(),
            _riskEngine.Object,
            Mock.Of<ILogger<LivePositionManager>>());
    }

    [TestMethod]
    public async Task GivenDeployGridSignal_WhenExecuteSignalsAsync_ThenCancelsExistingAndPlacesGridOrders()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["anchorPrice"] = 50000m,
                ["gridLevels"] = 3,
                ["gridSpacingPercent"] = 1.0m,
                ["notionalUsd"] = 100m,
                ["gridCycleId"] = "cycle-1",
                ["entryMode"] = EntryModes.AutoFromSignalCandle
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert — cancels existing, then places 3 grid levels
        _executionEngine.Verify(e => e.CancelAllOrdersAsync("BTC-PERP", It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.Side == OrderSide.Buy &&
                o.OrderType == OrderType.Limit &&
                o.TradeType == TradeType.GridFill),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task GivenDeployGridWithMarketEntry_WhenExecuteSignalsAsync_ThenPlacesMarketThenLimits()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["anchorPrice"] = 50000m,
                ["gridLevels"] = 3,
                ["gridSpacingPercent"] = 1.0m,
                ["notionalUsd"] = 100m,
                ["gridCycleId"] = "cycle-1",
                ["entryMode"] = EntryModes.InitialMarketThenGrid
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert — 1 market order + 2 limit orders (levels 2 and 3)
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o => o.OrderType == OrderType.Market),
            It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o => o.OrderType == OrderType.Limit),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GivenDeployGridSignalWithLeverage_WhenExecuteSignalsAsync_ThenSetsLeverageBeforeOrders()
    {
        var callOrder = new List<string>();
        _executionEngine.Setup(e => e.CancelAllOrdersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("CancelAllOrders"))
            .Returns(Task.CompletedTask);
        _executionEngine.Setup(e => e.SetLeverageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SetLeverage"))
            .Returns(Task.CompletedTask);
        _executionEngine.Setup(e => e.PlaceOrderAsync(It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("PlaceOrder"))
            .ReturnsAsync("order-1");

        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["anchorPrice"] = 50000m,
                ["gridLevels"] = 3,
                ["gridSpacingPercent"] = 1.0m,
                ["notionalUsd"] = 100m,
                ["gridCycleId"] = "cycle-1",
                ["entryMode"] = EntryModes.AutoFromSignalCandle,
                ["leverage"] = 33L,
                ["isIsolated"] = true,
            }
        };

        await _sut.ExecuteSignalsAsync([signal]);

        _executionEngine.Verify(e => e.SetLeverageAsync("BTC-PERP", 33, true, It.IsAny<CancellationToken>()), Times.Once);
        callOrder.IndexOf("SetLeverage").Should().BeGreaterOrEqualTo(0);
        callOrder.IndexOf("PlaceOrder").Should().BeGreaterOrEqualTo(0);
        callOrder.IndexOf("SetLeverage").Should().BeLessThan(callOrder.IndexOf("PlaceOrder"));
    }

    [TestMethod]
    public async Task GivenDeployGridSignalWithoutLeverage_WhenExecuteSignalsAsync_ThenDoesNotSetLeverage()
    {
        var signal = new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["anchorPrice"] = 50000m,
                ["gridLevels"] = 3,
                ["gridSpacingPercent"] = 1.0m,
                ["notionalUsd"] = 100m,
                ["gridCycleId"] = "cycle-1",
                ["entryMode"] = EntryModes.AutoFromSignalCandle
            }
        };

        await _sut.ExecuteSignalsAsync([signal]);

        _executionEngine.Verify(e => e.SetLeverageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenOpenPositionSignal_WhenExecuteSignalsAsync_ThenPlacesMarketOrder()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0.01m
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.OrderType == OrderType.Market &&
                o.Side == OrderSide.Buy &&
                o.Size == 0.01m &&
                o.TradeType == TradeType.SignalEntry),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenDcaOpenPositionSignal_WhenExecuteSignalsAsync_ThenPlacesSpotMarketOrder()
    {
        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-USD",
            Parameters = new Dictionary<string, object>
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0.01m,
                ["assetType"] = AssetType.Spot.ToString(),
                ["tradeType"] = TradeType.DcaBuy.ToString(),
                ["gridCycleId"] = "dca"
            }
        };

        await _sut.ExecuteSignalsAsync([signal]);

        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.Symbol == "BTC-USD" &&
                o.AssetType == AssetType.Spot &&
                o.OrderType == OrderType.Market &&
                o.Side == OrderSide.Buy &&
                o.TradeType == TradeType.DcaBuy),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenDcaOpenPositionSignalWithPerpSymbol_WhenExecuteSignalsAsync_ThenPlacesPerpMarketOrder()
    {
        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0.01m,
                ["assetType"] = AssetType.Perp.ToString(),
                ["tradeType"] = TradeType.DcaBuy.ToString(),
                ["gridCycleId"] = "dca"
            }
        };

        await _sut.ExecuteSignalsAsync([signal]);

        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.Symbol == "BTC-PERP" &&
                o.AssetType == AssetType.Perp &&
                o.OrderType == OrderType.Market &&
                o.Side == OrderSide.Buy &&
                o.TradeType == TradeType.DcaBuy),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenTakeProfitLimitSignal_WhenExecuteSignalsAsync_ThenCancelsAndPlacesSellOrder()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "TakeProfit",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["orderType"] = "Limit",
                ["targetPrice"] = 55000m,
                ["size"] = 0.01m,
                ["gridCycleId"] = "cycle-1"
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert — cancels existing then places sell limit
        _executionEngine.Verify(e => e.CancelAllOrdersAsync("BTC-PERP", It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.OrderType == OrderType.Limit &&
                o.Side == OrderSide.Sell &&
                o.Price == 55000m &&
                o.TradeType == TradeType.TakeProfit),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenTakeProfitMarketSignal_WhenExecuteSignalsAsync_ThenPlacesMarketSell()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "TakeProfit",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["orderType"] = "Market",
                ["size"] = 0.01m,
                ["gridCycleId"] = "cycle-1"
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.Is<OrderRequest>(o =>
                o.OrderType == OrderType.Market &&
                o.Side == OrderSide.Sell &&
                o.Price == 0m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenCancelGridSignal_WhenExecuteSignalsAsync_ThenCancelsAllOrders()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "CancelGrid",
            Symbol = "BTC-PERP"
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert
        _executionEngine.Verify(e => e.CancelAllOrdersAsync("BTC-PERP", It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenZeroSize_WhenOpenPosition_ThenDoesNotPlaceOrder()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "OpenPosition",
            Symbol = "BTC-PERP",
            Parameters = new Dictionary<string, object>
            {
                ["entryPrice"] = 50000m,
                ["size"] = 0m
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenNullSignals_WhenExecuteSignalsAsync_ThenThrowsArgumentNull()
    {
        // Act
        Func<Task> act = () => _sut.ExecuteSignalsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task GivenUnknownSignalType_WhenExecuteSignalsAsync_ThenDoesNotThrow()
    {
        // Arrange
        var signal = new TradingSignal
        {
            SignalType = "UnknownType",
            Symbol = "BTC-PERP"
        };

        // Act
        await _sut.ExecuteSignalsAsync([signal]);

        // Assert — no orders placed
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenMultipleSignals_WhenExecuteSignalsAsync_ThenProcessesAllInOrder()
    {
        // Arrange
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "CancelGrid",
                Symbol = "BTC-PERP"
            },
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["entryPrice"] = 50000m,
                    ["size"] = 0.01m
                }
            }
        };

        // Act
        await _sut.ExecuteSignalsAsync(signals);

        // Assert
        _executionEngine.Verify(e => e.CancelAllOrdersAsync("BTC-PERP", It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.PlaceOrderAsync(
            It.IsAny<OrderRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
