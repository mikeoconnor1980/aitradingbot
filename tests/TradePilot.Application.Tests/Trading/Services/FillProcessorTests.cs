using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Application.Trading.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Tests.Trading.Services;

[TestClass]
public sealed class FillProcessorTests
{
    private InMemoryOrderTracker _orderTracker = null!;
    private GridState _gridState = null!;
    private Mock<IRiskEngine> _riskEngine = null!;
    private Mock<ILiveOrderRepository> _orderRepository = null!;
    private Mock<ILiveFillRepository> _fillRepository = null!;
    private Mock<IGridCycleRepository> _gridCycleRepository = null!;
    private FillProcessor _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderTracker = new InMemoryOrderTracker();
        _gridState = new GridState
        {
            Lifecycle = GridLifecycle.Active,
            GridCycleId = "cycle-1",
            TotalLevels = 3,
            FilledLevels = 0
        };
        _riskEngine = new Mock<IRiskEngine>();
        _orderRepository = new Mock<ILiveOrderRepository>();
        _fillRepository = new Mock<ILiveFillRepository>();
        _gridCycleRepository = new Mock<IGridCycleRepository>();

        _sut = new FillProcessor(
            _orderTracker,
            _gridState,
            Mock.Of<ILogger<FillProcessor>>(),
            _riskEngine.Object,
            _orderRepository.Object,
            _fillRepository.Object,
            _gridCycleRepository.Object,
            "test-user");
    }

    [TestMethod]
    public async Task GivenGridFill_WhenProcessFillAsync_ThenRecordsOrderClosedOnRiskEngine()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);

        var fill = CreateFill("order-1", closedPnl: 0m);

        // Act
        await _sut.ProcessFillAsync(fill);

        // Assert
        _riskEngine.Verify(r => r.RecordOrdersClosed(1), Times.Once);
        _riskEngine.Verify(r => r.RecordLoss(It.IsAny<decimal>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenFillWithNegativePnl_WhenProcessFillAsync_ThenRecordsLossOnRiskEngine()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.TakeProfit);

        var fill = CreateFill("order-1", closedPnl: -25.50m);

        // Act
        await _sut.ProcessFillAsync(fill);

        // Assert
        _riskEngine.Verify(r => r.RecordLoss(25.50m), Times.Once);
        _riskEngine.Verify(r => r.RecordOrdersClosed(1), Times.Once);
    }

    [TestMethod]
    public async Task GivenFillWithPositivePnl_WhenProcessFillAsync_ThenDoesNotRecordLoss()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.TakeProfit);

        var fill = CreateFill("order-1", closedPnl: 10.00m);

        // Act
        await _sut.ProcessFillAsync(fill);

        // Assert
        _riskEngine.Verify(r => r.RecordLoss(It.IsAny<decimal>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenFill_WhenProcessFillAsync_ThenOnFillProcessedCallbackInvoked()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);

        FillEventDto? callbackFill = null;
        _sut.OnFillProcessed = fill =>
        {
            callbackFill = fill;
            return Task.CompletedTask;
        };

        var fill = CreateFill("order-1");

        // Act
        await _sut.ProcessFillAsync(fill);

        // Assert
        callbackFill.Should().NotBeNull();
        callbackFill!.OrderId.Should().Be("order-1");
    }

    [TestMethod]
    public async Task GivenAsyncFillCallback_WhenProcessFillAsync_ThenAwaitedBeforeReturning()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);

        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompleted = false;

        _sut.OnFillProcessed = async _ =>
        {
            callbackStarted.TrySetResult();
            await allowCompletion.Task;
            callbackCompleted = true;
        };

        var fill = CreateFill("order-1");

        // Act
        var processTask = _sut.ProcessFillAsync(fill);
        await callbackStarted.Task;

        // Assert
        processTask.IsCompleted.Should().BeFalse();

        allowCompletion.TrySetResult();
        await processTask;

        callbackCompleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenMultipleFills_WhenGridCycleClosed_ThenRealisedPnlAccumulates()
    {
        // Arrange
        var cycle = new GridCycle
        {
            Id = Guid.NewGuid(),
            GridCycleId = "cycle-1",
            RealisedPnl = null,
            Lifecycle = "Active"
        };
        _gridCycleRepository
            .Setup(r => r.GetByGridCycleIdAsync("cycle-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cycle);

        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);
        _orderTracker.TrackOrder("order-2", "cycle-1", 2, "BTC-PERP",
            OrderSide.Buy, 49500m, 0.01m, TradeType.GridFill);

        // Act — first fill
        await _sut.ProcessFillAsync(CreateFill("order-1", closedPnl: 5.00m));

        // Assert — PnL should be 5.00
        cycle.RealisedPnl.Should().Be(5.00m);

        // Act — second fill
        await _sut.ProcessFillAsync(CreateFill("order-2", closedPnl: 3.50m));

        // Assert — PnL should accumulate to 8.50
        cycle.RealisedPnl.Should().Be(8.50m);
    }

    [TestMethod]
    public async Task GivenCancelledOrder_WhenProcessOrderUpdateAsync_ThenRecordsOrderClosedOnRiskEngine()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);

        var update = new OrderUpdateDto
        {
            OrderId = "order-1",
            Status = "canceled"
        };

        // Act
        await _sut.ProcessOrderUpdateAsync(update);

        // Assert
        _riskEngine.Verify(r => r.RecordOrdersClosed(1), Times.Once);
    }

    [TestMethod]
    public async Task GivenGridFill_WhenProcessFillAsync_ThenPersistsFillToRepository()
    {
        // Arrange
        _orderTracker.TrackOrder("order-1", "cycle-1", 1, "BTC-PERP",
            OrderSide.Buy, 50000m, 0.01m, TradeType.GridFill);

        var fill = CreateFill("order-1");

        // Act
        await _sut.ProcessFillAsync(fill);

        // Assert
        _fillRepository.Verify(r => r.AddAsync(
            It.Is<LiveFill>(f =>
                f.OrderId == "order-1" &&
                f.Symbol == "BTC" &&
                f.Side == OrderSide.Buy &&
                f.UserId == "test-user"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FillEventDto CreateFill(string orderId, decimal closedPnl = 0m)
    {
        return new FillEventDto
        {
            OrderId = orderId,
            Asset = "BTC",
            Side = "Buy",
            Direction = "Long",
            Price = 50000m,
            Size = 0.01m,
            Fee = 0.05m,
            ClosedPnl = closedPnl,
            Timestamp = DateTime.UtcNow
        };
    }
}
