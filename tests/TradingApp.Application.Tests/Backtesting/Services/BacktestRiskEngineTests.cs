using Microsoft.Extensions.Options;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestRiskEngineTests
{
    private RiskLimitsConfig _limits = null!;
    private BacktestRiskEngine _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _limits = new RiskLimitsConfig { MaxPortfolioHeatPercent = 6m };
        _sut = new BacktestRiskEngine(Options.Create(_limits));
    }

    [TestMethod]
    public async Task GivenHeatBelowLimit_WhenEntrySignalValidated_ThenAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC", 100m);

        var approvedSignals = await _sut.ValidateAsync([CreateEntrySignal("ETH", 100m)]);

        approvedSignals.Should().HaveCount(1);
        _sut.HeatBlockedSignalCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenHeatExceedsLimit_WhenEntrySignalValidated_ThenBlockedAndCounted()
    {
        _sut.UpdatePortfolioState(10_000m);
        for (var index = 0; index < 6; index++)
        {
            _sut.RecordPositionOpened($"TOKEN{index}", 100m);
        }

        var approvedSignals = await _sut.ValidateAsync([CreateEntrySignal("NEW", 100m)]);

        approvedSignals.Should().BeEmpty();
        _sut.HeatBlockedSignalCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenRiskReducingSignal_WhenHeatAtLimit_ThenSignalPasses()
    {
        _sut.UpdatePortfolioState(10_000m);
        _sut.RecordPositionOpened("BTC", 600m);

        var approvedSignals = await _sut.ValidateAsync([
            new TradingSignal { SignalType = "TakeProfit", Symbol = "BTC" }
        ]);

        approvedSignals.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task GivenHeatDisabled_WhenEntrySignalValidated_ThenAllowed()
    {
        _limits = _limits with { MaxPortfolioHeatPercent = 0m };
        _sut = new BacktestRiskEngine(Options.Create(_limits));
        _sut.UpdatePortfolioState(10_000m);

        var approvedSignals = await _sut.ValidateAsync([CreateEntrySignal("BTC", 1_000m)]);

        approvedSignals.Should().HaveCount(1);
        _sut.HeatBlockedSignalCount.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenPositionCloseSignal_WhenTrackedRiskRemoved_ThenEntryAllowed()
    {
        _sut.UpdatePortfolioState(10_000m);
        for (var index = 0; index < 6; index++)
        {
            _sut.RecordPositionOpened($"TOKEN{index}", 100m);
        }

        var exitSignals = await _sut.ValidateAsync([
            new TradingSignal { SignalType = "TakeProfit", Symbol = "TOKEN0" }
        ]);
        var entrySignals = await _sut.ValidateAsync([CreateEntrySignal("NEW", 100m)]);

        exitSignals.Should().HaveCount(1);
        entrySignals.Should().HaveCount(1);
    }

    private static TradingSignal CreateEntrySignal(string symbol, decimal estimatedRiskUsd)
    {
        return new TradingSignal
        {
            SignalType = "DeployGrid",
            Symbol = symbol,
            Parameters = new Dictionary<string, object>
            {
                ["notionalUsd"] = 1_000m,
                ["gridLevels"] = 1,
                ["estimatedRiskUsd"] = estimatedRiskUsd,
            }
        };
    }
}