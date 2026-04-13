using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Application.Trading.Services;

namespace TradingApp.Application.Tests.Trading.Services;

[TestClass]
public sealed class LiveRiskEngineTests
{
    private LiveRiskEngine _sut = null!;
    private RiskLimitsConfig _limits = null!;

    [TestInitialize]
    public void Setup()
    {
        _limits = new RiskLimitsConfig
        {
            MaxDailyLossUsd = 500m,
            MaxOpenOrders = 10,
            MaxOrderSizeUsd = 5_000m,
            CircuitBreakerCooldownMinutes = 60,
            MaxPortfolioHeatPercent = 6m,
        };

        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);
    }

    [TestMethod]
    public async Task GivenEmptySignals_WhenValidateAsync_ThenReturnsEmpty()
    {
        var result = await _sut.ValidateAsync([]);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenNormalSignal_WhenValidateAsync_ThenPassesThrough()
    {
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenCircuitBreakerTripped_WhenValidateTakeProfit_ThenPassesThrough()
    {
        // Trip the circuit breaker
        _sut.RecordLoss(600m);
        _sut.IsCircuitBreakerTripped.Should().BeTrue();

        var signals = new List<TradingSignal>
        {
            new() { SignalType = "TakeProfit", Symbol = "BTC-PERP" }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenCircuitBreakerTripped_WhenValidateCancelGrid_ThenPassesThrough()
    {
        _sut.RecordLoss(600m);

        var signals = new List<TradingSignal>
        {
            new() { SignalType = "CancelGrid", Symbol = "BTC-PERP" }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenCircuitBreakerTripped_WhenValidateNewOrder_ThenBlocked()
    {
        _sut.RecordLoss(600m);

        var signals = new List<TradingSignal>
        {
            new() { SignalType = "DeployGrid", Symbol = "BTC-PERP" }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenOrderSizeExceedsMax_WhenValidateAsync_ThenBlocked()
    {
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["notionalUsd"] = 10_000m }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenOrderSizeWithinLimit_WhenValidateAsync_ThenPasses()
    {
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["notionalUsd"] = 3_000m }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenDeployGridExceedsMaxOpenOrders_WhenValidateAsync_ThenBlocked()
    {
        // Pre-fill 8 orders (max is 10)
        _sut.RecordOrdersPlaced(8);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenDeployGridWithinLimit_WhenValidateAsync_ThenPasses()
    {
        _sut.RecordOrdersPlaced(3);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public void GivenLossBelowThreshold_WhenRecordLoss_ThenCircuitBreakerNotTripped()
    {
        _sut.RecordLoss(100m);

        _sut.IsCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public void GivenLossExceedsThreshold_WhenRecordLoss_ThenCircuitBreakerTripped()
    {
        _sut.RecordLoss(300m);
        _sut.RecordLoss(250m);

        _sut.IsCircuitBreakerTripped.Should().BeTrue();
    }

    [TestMethod]
    public void GivenZeroOrNegativeLoss_WhenRecordLoss_ThenIgnored()
    {
        _sut.RecordLoss(0m);
        _sut.RecordLoss(-50m);

        _sut.GetRollingDailyLoss().Should().Be(0m);
    }

    [TestMethod]
    public void GivenOrdersPlaced_WhenRecordOrdersPlaced_ThenCountTracked()
    {
        _sut.RecordOrdersPlaced(5);
        _sut.ActiveOrderCount.Should().Be(5);
    }

    [TestMethod]
    public void GivenOrdersPlacedAndClosed_WhenRecordOrdersClosed_ThenCountDecremented()
    {
        _sut.RecordOrdersPlaced(5);
        _sut.RecordOrdersClosed(3);

        _sut.ActiveOrderCount.Should().Be(2);
    }

    [TestMethod]
    public void GivenNoOrdersPlaced_WhenRecordOrdersClosed_ThenCountStaysAtZero()
    {
        _sut.RecordOrdersClosed(10);

        _sut.ActiveOrderCount.Should().Be(0);
    }

    [TestMethod]
    public void GivenCircuitBreakerTripped_WhenResetCircuitBreaker_ThenCleared()
    {
        _sut.RecordLoss(600m);
        _sut.IsCircuitBreakerTripped.Should().BeTrue();

        _sut.ResetCircuitBreaker();

        _sut.IsCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public void GivenPortfolioStateUpdate_WhenCalled_ThenTracksEquity()
    {
        _sut.UpdatePortfolioState(10_000m);

        _sut.TrackedEquity.Should().Be(10_000m);
    }

    [TestMethod]
    public void GivenDrawdownStateUpdated_WhenCalled_ThenTracksScalingFactor()
    {
        _sut.UpdateDrawdownState(0.5m, isHalted: false);

        _sut.DrawdownScalingFactor.Should().Be(0.5m);
        _sut.IsDrawdownCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public void GivenPositionLifecycleUpdates_WhenCalled_ThenTracksPositions()
    {
        _sut.RecordPositionOpened("BTC-PERP", 100m);
        _sut.RecordPositionOpened("ETH-PERP", 50m);

        _sut.TrackedPositionCount.Should().Be(2);

        _sut.RecordPositionClosed("BTC-PERP");

        _sut.TrackedPositionCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenHeatBelowLimit_WhenEntrySignalValidated_ThenAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("ETH-PERP", 500m);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["gridLevels"] = 3,
                    ["estimatedRiskUsd"] = 100m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
        _sut.TrackedPositionCount.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenHeatAboveLimit_WhenEntrySignalValidated_ThenBlocked()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("ETH-PERP", 600m);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["gridLevels"] = 2,
                    ["estimatedRiskUsd"] = 100m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
        _sut.TrackedPositionCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenHeatAtLimit_WhenRiskReducingSignalValidated_ThenAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC-PERP", 600m);

        var signals = new List<TradingSignal>
        {
            new() { SignalType = "TakeProfit", Symbol = "BTC-PERP" }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenHeatLimitDisabled_WhenEntrySignalValidated_ThenAllowed()
    {
        _limits = _limits with { MaxPortfolioHeatPercent = 0m };
        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("ETH-PERP", 600m);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["gridLevels"] = 2,
                    ["estimatedRiskUsd"] = 200m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenPositionClosed_WhenHeatRechecked_ThenEntryAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("ETH-PERP", 600m);
        _sut.RecordPositionClosed("ETH-PERP");

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["gridLevels"] = 2,
                    ["estimatedRiskUsd"] = 100m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenFlattenPositionSignal_WhenValidated_ThenTrackedRiskRemoved()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC-PERP", 100m);

        var signals = new List<TradingSignal>
        {
            new() { SignalType = "FlattenPosition", Symbol = "BTC-PERP" }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
        _sut.TrackedPositionCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenSignalWithoutEstimatedRisk_WhenHeatChecked_ThenAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("ETH-PERP", 600m);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["entryPrice"] = 50_000m,
                    ["size"] = 0.1m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenMixedSignals_WhenValidateAsync_ThenApprovesCorrectSubset()
    {
        _sut.RecordOrdersPlaced(9);

        var signals = new List<TradingSignal>
        {
            // Should pass — risk-reducing
            new() { SignalType = "TakeProfit", Symbol = "BTC-PERP" },
            // Should be blocked — 5 levels + 9 existing = 14 > max 10
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 5 }
            },
            // Should pass — no notionalUsd parameter, so size check is skipped (backward compatible)
            new() { SignalType = "OpenPosition", Symbol = "ETH-PERP" },
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(2);
        result.Select(s => s.SignalType).Should().Contain("TakeProfit");
        result.Select(s => s.SignalType).Should().Contain("OpenPosition");
    }

    [TestMethod]
    public async Task GivenMaxOrderSize1000_WhenDeployGridNotionalUsd1500_ThenBlocked()
    {
        _limits = _limits with { MaxOrderSizeUsd = 1_000m };
        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["notionalUsd"] = 1_500m,
                    ["gridLevels"] = 5,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenMaxOrderSize1000_WhenDeployGridNotionalUsd800_ThenApproved()
    {
        _limits = _limits with { MaxOrderSizeUsd = 1_000m };
        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["notionalUsd"] = 800m,
                    ["gridLevels"] = 5,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenMaxOrderSize1000_WhenOpenPositionNotionalUsd1500_ThenBlocked()
    {
        _limits = _limits with { MaxOrderSizeUsd = 1_000m };
        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["notionalUsd"] = 1_500m,
                    ["size"] = 0.5m,
                    ["entryPrice"] = 3_000m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenSignalWithNoNotionalUsd_WhenValidated_ThenPassesBackwardCompatible()
    {
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "OpenPosition",
                Symbol = "ETH-PERP",
                Parameters = new Dictionary<string, object>
                {
                    ["size"] = 1m,
                    ["entryPrice"] = 2_000m,
                }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenDrawdownCircuitBreakerTripped_WhenDeployGridSignalValidated_ThenBlocked()
    {
        _sut.UpdateDrawdownState(0m, isHalted: true);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenDrawdownCircuitBreakerTripped_WhenTakeProfitSignalValidated_ThenPassesThrough()
    {
        _sut.UpdateDrawdownState(0m, isHalted: true);

        var result = await _sut.ValidateAsync(
        [
            new TradingSignal { SignalType = "TakeProfit", Symbol = "BTC-PERP" }
        ]);

        result.Should().ContainSingle();
    }

    [TestMethod]
    public async Task GivenDailyLossCircuitBreakerTrippedButDrawdownNot_WhenSignalValidated_ThenBlockedByDailyLoss()
    {
        _sut.RecordLoss(_limits.MaxDailyLossUsd + 1m);
        _sut.UpdateDrawdownState(1.0m, isHalted: false);

        var result = await _sut.ValidateAsync(
        [
            new TradingSignal { SignalType = "DeployGrid", Symbol = "BTC-PERP" }
        ]);

        result.Should().BeEmpty();
        _sut.IsCircuitBreakerTripped.Should().BeTrue();
        _sut.IsDrawdownCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenDrawdownCircuitBreakerReset_WhenDeployGridSignalValidated_ThenPasses()
    {
        _sut.UpdateDrawdownState(0m, isHalted: true);
        _sut.UpdateDrawdownState(0.5m, isHalted: false);

        var result = await _sut.ValidateAsync(
        [
            new TradingSignal
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["gridLevels"] = 2 }
            }
        ]);

        result.Should().ContainSingle();
    }
}
