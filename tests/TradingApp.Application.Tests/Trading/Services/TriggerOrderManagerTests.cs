using Microsoft.Extensions.Logging;
using Moq;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class TriggerOrderManagerTests
{
    private Mock<IExecutionEngine> _executionEngine = null!;
    private TriggerOrderManager _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _executionEngine = new Mock<IExecutionEngine>();
        _sut = new TriggerOrderManager(
            _executionEngine.Object,
            Mock.Of<ILogger<TriggerOrderManager>>());
    }

    // ──────────────────────────────────────────────────────────────
    // CalculateStopLossPrice
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void GivenDisabledStopLoss_WhenCalculateStopLossPrice_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition();
        var config = new ExitRuleConfig { Enabled = false };
        var context = CreateContext();

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenClosedPosition_WhenCalculateStopLossPrice_ThenReturnsNull()
    {
        // Arrange
        var position = new PositionState { Size = 0, AverageEntryPrice = 0 };
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m };
        var context = CreateContext();

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenLongPosition_WhenCalculateFixedPercentSL_ThenReturnsPriceBelowEntry()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m };
        var context = CreateContext();

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(95m); // 100 * (1 - 5/100)
    }

    [TestMethod]
    public void GivenShortPosition_WhenCalculateFixedPercentSL_ThenReturnsPriceAboveEntry()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 5m };
        var context = CreateContext();

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(105m); // 100 * (1 + 5/100)
    }

    [TestMethod]
    public void GivenLongPosition_WhenCalculateAtrTrailingSL_ThenUsesHighMinusAtrMultiple()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrTrailing, AtrMultiplier = 2m };
        var context = CreateContext(candleHigh: 110m, atr: 5m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(100m); // 110 - (5 * 2)
    }

    [TestMethod]
    public void GivenShortPosition_WhenCalculateAtrTrailingSL_ThenUsesHighPlusAtrMultiple()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrTrailing, AtrMultiplier = 2m };
        var context = CreateContext(candleHigh: 105m, atr: 5m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(115m); // 105 + (5 * 2)
    }

    [TestMethod]
    public void GivenZeroAtr_WhenCalculateAtrTrailingSL_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition();
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrTrailing, AtrMultiplier = 2m };
        var context = CreateContext(atr: 0m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenLongPosition_WhenCalculateAtrInitialSL_ThenUsesEntryMinusAtrMultiple()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
        var context = CreateContext(atr: 500m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(49_000m);
    }

    [TestMethod]
    public void GivenShortPosition_WhenCalculateAtrInitialSL_ThenUsesEntryPlusAtrMultiple()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
        var context = CreateContext(atr: 500m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(51_000m);
    }

    [TestMethod]
    public void GivenAtrInitialWithZeroAtrAndFallbackValue_WhenCalculateStopLossPrice_ThenUsesFixedPercent()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m, Value = 2m };
        var context = CreateContext(atr: 0m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(49_000m);
    }

    [TestMethod]
    public void GivenAtrInitialWithZeroAtrAndNoFallback_WhenCalculateStopLossPrice_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrInitial, AtrMultiplier = 2m };
        var context = CreateContext(atr: 0m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenNoAtrMultiplier_WhenCalculateAtrTrailingSL_ThenDefaultsTo3x()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.AtrTrailing };
        var context = CreateContext(candleHigh: 110m, atr: 2m);

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().Be(104m); // 110 - (2 * 3)
    }

    [TestMethod]
    public void GivenNoValue_WhenCalculateFixedPercentSL_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition();
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = null };
        var context = CreateContext();

        // Act
        var result = TriggerOrderManager.CalculateStopLossPrice(position, config, context);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // CalculateTakeProfitPrice
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void GivenDisabledTakeProfit_WhenCalculateTakeProfitPrice_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition();
        var config = new ExitRuleConfig { Enabled = false };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenLongPosition_WhenCalculateFixedPercentTP_ThenReturnsPriceAboveEntry()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Value = 10m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config);

        // Assert
        result.Should().Be(110m); // 100 * (1 + 10/100)
    }

    [TestMethod]
    public void GivenShortPosition_WhenCalculateFixedPercentTP_ThenReturnsPriceBelowEntry()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 100m);
        var config = new ExitRuleConfig { Enabled = true, Value = 10m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config);

        // Assert
        result.Should().Be(90m); // 100 * (1 - 10/100)
    }

    [TestMethod]
    public void GivenClosedPosition_WhenCalculateTakeProfitPrice_ThenReturnsNull()
    {
        // Arrange
        var position = new PositionState { Size = 0, AverageEntryPrice = 0 };
        var config = new ExitRuleConfig { Enabled = true, Value = 10m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenNoTPValue_WhenCalculateTakeProfitPrice_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition();
        var config = new ExitRuleConfig { Enabled = true, Value = null };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenLongPosition_WhenCalculateRMultipleTP_ThenReturnsCorrectPrice()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config, stopLossPercent: 2m);

        // Assert
        result.Should().Be(52_000m);
    }

    [TestMethod]
    public void GivenShortPosition_WhenCalculateRMultipleTP_ThenReturnsCorrectPrice()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 3m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config, stopLossPercent: 2m);

        // Assert
        result.Should().Be(47_000m);
    }

    [TestMethod]
    public void GivenRMultipleTP_WhenStopLossPercentNull_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config, stopLossPercent: null);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void GivenRMultipleTP_WhenStopLossPercentZero_ThenReturnsNull()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m);
        var config = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.RMultiple, Value = 2m };

        // Act
        var result = TriggerOrderManager.CalculateTakeProfitPrice(position, config, stopLossPercent: 0m);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // PlaceProtectionOrdersAsync
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GivenOpenPosition_WhenPlaceProtection_ThenPlacesBothTriggers()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext(symbol: "ETH");
        var protectionState = new ProtectionOrderState();

        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync("ETH", "sell", 1m, 95m, "sl", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sl-order-1");
        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync("ETH", "sell", 1m, 110m, "tp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tp-order-1");

        // Act
        await _sut.PlaceProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        protectionState.StopLossOrderId.Should().Be("sl-order-1");
        protectionState.StopLossTriggerPrice.Should().Be(95m);
        protectionState.TakeProfitOrderId.Should().Be("tp-order-1");
        protectionState.TakeProfitTriggerPrice.Should().Be(110m);
        protectionState.LastUpdatedAtUtc.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GivenClosedPosition_WhenPlaceProtection_ThenSkips()
    {
        // Arrange
        var position = new PositionState { Size = 0, AverageEntryPrice = 0 };
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext();
        var protectionState = new ProtectionOrderState();

        // Act
        await _sut.PlaceProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.PlaceTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenExistingSLOrder_WhenPlaceProtection_ThenOnlyPlacesTP()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext(symbol: "ETH");
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "existing-sl",
            StopLossTriggerPrice = 95m
        };

        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync("ETH", "sell", 1m, 110m, "tp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tp-order-1");

        // Act
        await _sut.PlaceProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.PlaceTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), "sl",
                It.IsAny<CancellationToken>()),
            Times.Never);
        protectionState.TakeProfitOrderId.Should().Be("tp-order-1");
    }

    [TestMethod]
    public async Task GivenShortPosition_WhenPlaceProtection_ThenUsesBuySide()
    {
        // Arrange
        var position = CreateShortPosition(entryPrice: 100m, size: 2m);
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext(symbol: "BTC");
        var protectionState = new ProtectionOrderState();

        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync("BTC", "buy", 2m, 105m, "sl", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sl-short");
        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync("BTC", "buy", 2m, 90m, "tp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tp-short");

        // Act
        await _sut.PlaceProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        protectionState.StopLossOrderId.Should().Be("sl-short");
        protectionState.StopLossTriggerPrice.Should().Be(105m);
        protectionState.TakeProfitOrderId.Should().Be("tp-short");
        protectionState.TakeProfitTriggerPrice.Should().Be(90m);
    }

    [TestMethod]
    public async Task GivenExchangeRejectsOrder_WhenPlaceProtection_ThenDoesNotSetState()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext(symbol: "ETH");
        var protectionState = new ProtectionOrderState();

        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), "sl", It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), "tp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tp-order-1");

        // Act
        await _sut.PlaceProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        protectionState.HasStopLoss.Should().BeFalse();
        protectionState.HasTakeProfit.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenExchangeThrows_WhenPlaceProtection_ThenDoesNotThrow()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slPercent: 5m);
        var context = CreateContext(symbol: "ETH");
        var protectionState = new ProtectionOrderState();

        _executionEngine
            .Setup(e => e.PlaceTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        // Act
        var act = () => _sut.PlaceProtectionOrdersAsync(
            position, exitConfig, context, protectionState);

        // Assert
        await act.Should().NotThrowAsync();
        protectionState.HasAny.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // UpdateProtectionOrdersAsync
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GivenSamePrices_WhenUpdateProtection_ThenSkipsModification()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slPercent: 5m, tpPercent: 10m);
        var context = CreateContext();
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "sl-1",
            StopLossTriggerPrice = 95m,
            TakeProfitOrderId = "tp-1",
            TakeProfitTriggerPrice = 110m
        };

        // Act
        await _sut.UpdateProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.ModifyTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenChangedSLPrice_WhenUpdateProtection_ThenModifiesSLOrder()
    {
        // Arrange — ATR trailing SL moved from 95 to 100
        var position = CreateLongPosition(entryPrice: 100m, size: 1m);
        var exitConfig = CreateExitConfig(slAtrTrailing: true, atrMultiplier: 2m, tpPercent: 10m);
        var context = CreateContext(symbol: "ETH", candleHigh: 115m, atr: 5m);
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "sl-1",
            StopLossTriggerPrice = 95m,
            TakeProfitOrderId = "tp-1",
            TakeProfitTriggerPrice = 110m
        };

        // Act
        await _sut.UpdateProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert — SL should be modified to 115 - (5 * 2) = 105
        _executionEngine.Verify(
            e => e.ModifyTriggerOrderAsync("sl-1", "ETH", "sell", 105m, 1m, "sl",
                It.IsAny<CancellationToken>()),
            Times.Once);
        protectionState.StopLossTriggerPrice.Should().Be(105m);
    }

    [TestMethod]
    public async Task GivenAtrInitialStopLoss_WhenUpdateProtectionOrdersAsync_ThenStopLossNotModified()
    {
        // Arrange
        var position = CreateLongPosition(entryPrice: 50_000m, size: 1m);
        var exitConfig = new ExitConfig
        {
            StopLoss = new ExitRuleConfig
            {
                Enabled = true,
                Type = ExitRuleType.AtrInitial,
                AtrMultiplier = 2m,
            },
            TakeProfit = new ExitRuleConfig
            {
                Enabled = false,
                Type = ExitRuleType.FixedPercent,
            }
        };
        var context = CreateContext(symbol: "ETH", atr: 800m);
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "sl-1",
            StopLossTriggerPrice = 49_000m,
        };

        // Act
        await _sut.UpdateProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.ModifyTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        protectionState.StopLossTriggerPrice.Should().Be(49_000m);
    }

    [TestMethod]
    public async Task GivenClosedPosition_WhenUpdateProtection_ThenSkips()
    {
        // Arrange
        var position = new PositionState { Size = 0, AverageEntryPrice = 0 };
        var exitConfig = CreateExitConfig(slPercent: 5m);
        var context = CreateContext();
        var protectionState = new ProtectionOrderState { StopLossOrderId = "sl-1", StopLossTriggerPrice = 95m };

        // Act
        await _sut.UpdateProtectionOrdersAsync(position, exitConfig, context, protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.ModifyTriggerOrderAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────────
    // CancelProtectionOrdersAsync
    // ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GivenBothOrders_WhenCancelProtection_ThenCancelsBothAndClears()
    {
        // Arrange
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "sl-1",
            StopLossTriggerPrice = 95m,
            TakeProfitOrderId = "tp-1",
            TakeProfitTriggerPrice = 110m
        };

        // Act
        await _sut.CancelProtectionOrdersAsync(protectionState);

        // Assert
        _executionEngine.Verify(e => e.CancelOrderAsync("sl-1", It.IsAny<CancellationToken>()), Times.Once);
        _executionEngine.Verify(e => e.CancelOrderAsync("tp-1", It.IsAny<CancellationToken>()), Times.Once);
        protectionState.HasAny.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenNoOrders_WhenCancelProtection_ThenSkips()
    {
        // Arrange
        var protectionState = new ProtectionOrderState();

        // Act
        await _sut.CancelProtectionOrdersAsync(protectionState);

        // Assert
        _executionEngine.Verify(
            e => e.CancelOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenCancelThrows_WhenCancelProtection_ThenDoesNotThrow()
    {
        // Arrange
        var protectionState = new ProtectionOrderState
        {
            StopLossOrderId = "sl-1",
            StopLossTriggerPrice = 95m
        };

        _executionEngine
            .Setup(e => e.CancelOrderAsync("sl-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        // Act
        var act = () => _sut.CancelProtectionOrdersAsync(protectionState);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static PositionState CreateLongPosition(decimal entryPrice = 100m, decimal size = 1m)
        => new() { Size = size, AverageEntryPrice = entryPrice };

    private static PositionState CreateShortPosition(decimal entryPrice = 100m, decimal size = 1m)
        => new() { Size = -size, AverageEntryPrice = entryPrice };

    private static MarketContext CreateContext(
        string symbol = "ETH",
        decimal candleHigh = 105m,
        decimal atr = 2m)
        => new()
        {
            Symbol = symbol,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CurrentCandle = Candle.Create(
                symbol,
                "15m",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                100m,
                candleHigh,
                99m,
                102m,
                1000m,
                100),
            Indicators = new IndicatorSnapshot { Atr = atr }
        };

    private static ExitConfig CreateExitConfig(
        decimal? slPercent = null,
        decimal? tpPercent = null,
        bool slAtrTrailing = false,
        decimal? atrMultiplier = null)
        => new()
        {
            StopLoss = new ExitRuleConfig
            {
                Enabled = slPercent.HasValue || slAtrTrailing,
                Type = slAtrTrailing ? ExitRuleType.AtrTrailing : ExitRuleType.FixedPercent,
                Value = slPercent,
                AtrMultiplier = atrMultiplier
            },
            TakeProfit = new ExitRuleConfig
            {
                Enabled = tpPercent.HasValue,
                Type = ExitRuleType.FixedPercent,
                Value = tpPercent
            }
        };
}
