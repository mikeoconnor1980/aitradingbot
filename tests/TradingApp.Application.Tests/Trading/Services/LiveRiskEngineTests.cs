using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
        };

        _sut = new LiveRiskEngine(
            Options.Create(_limits),
            new Mock<ILogger<LiveRiskEngine>>().Object);
    }

    [TestMethod]
    public async Task ValidateAsync_EmptySignals_ReturnsEmpty()
    {
        var result = await _sut.ValidateAsync([]);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ValidateAsync_NormalSignal_PassesThrough()
    {
        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["levels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task ValidateAsync_TakeProfitAlwaysPasses_EvenWhenCircuitBreakerTripped()
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
    public async Task ValidateAsync_CancelGridAlwaysPasses_EvenWhenCircuitBreakerTripped()
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
    public async Task ValidateAsync_CircuitBreakerTripped_BlocksNewOrders()
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
    public async Task ValidateAsync_OrderSizeExceedsMax_Blocked()
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
    public async Task ValidateAsync_OrderSizeWithinLimit_Passes()
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
    public async Task ValidateAsync_DeployGridExceedsMaxOpenOrders_Blocked()
    {
        // Pre-fill 8 orders (max is 10)
        _sut.RecordOrdersPlaced(8);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["levels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ValidateAsync_DeployGridWithinLimit_Passes()
    {
        _sut.RecordOrdersPlaced(3);

        var signals = new List<TradingSignal>
        {
            new()
            {
                SignalType = "DeployGrid",
                Symbol = "BTC-PERP",
                Parameters = new Dictionary<string, object> { ["levels"] = 5 }
            }
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(1);
    }

    [TestMethod]
    public void RecordLoss_BelowThreshold_DoesNotTripCircuitBreaker()
    {
        _sut.RecordLoss(100m);

        _sut.IsCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public void RecordLoss_ExceedsThreshold_TripsCircuitBreaker()
    {
        _sut.RecordLoss(300m);
        _sut.RecordLoss(250m);

        _sut.IsCircuitBreakerTripped.Should().BeTrue();
    }

    [TestMethod]
    public void RecordLoss_ZeroOrNegative_Ignored()
    {
        _sut.RecordLoss(0m);
        _sut.RecordLoss(-50m);

        _sut.GetRollingDailyLoss().Should().Be(0m);
    }

    [TestMethod]
    public void RecordOrdersPlaced_TracksCount()
    {
        _sut.RecordOrdersPlaced(5);
        _sut.ActiveOrderCount.Should().Be(5);
    }

    [TestMethod]
    public void RecordOrdersClosed_DecrementsCount()
    {
        _sut.RecordOrdersPlaced(5);
        _sut.RecordOrdersClosed(3);

        _sut.ActiveOrderCount.Should().Be(2);
    }

    [TestMethod]
    public void RecordOrdersClosed_NeverGoesNegative()
    {
        _sut.RecordOrdersClosed(10);

        _sut.ActiveOrderCount.Should().Be(0);
    }

    [TestMethod]
    public void ResetCircuitBreaker_ClearsTrip()
    {
        _sut.RecordLoss(600m);
        _sut.IsCircuitBreakerTripped.Should().BeTrue();

        _sut.ResetCircuitBreaker();

        _sut.IsCircuitBreakerTripped.Should().BeFalse();
    }

    [TestMethod]
    public async Task ValidateAsync_MixedSignals_ApprovesCorrectSubset()
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
                Parameters = new Dictionary<string, object> { ["levels"] = 5 }
            },
            // Should pass — no notionalUsd parameter so size check is skipped
            new() { SignalType = "OpenPosition", Symbol = "ETH-PERP" },
        };

        var result = await _sut.ValidateAsync(signals);

        result.Should().HaveCount(2);
        result.Select(s => s.SignalType).Should().Contain("TakeProfit");
        result.Select(s => s.SignalType).Should().Contain("OpenPosition");
    }
}
