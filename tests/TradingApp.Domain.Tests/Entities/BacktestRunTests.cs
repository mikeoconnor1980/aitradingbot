using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.Domain.Tests.Entities;

[TestClass]
public sealed class BacktestRunTests
{
    [TestMethod]
    public void GivenNoStrategyId_WhenCreateQueued_ThenStrategyFieldsAreNull()
    {
        var run = BacktestRun.CreateQueued(
            symbol: "BTC-USD",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 10000m);

        run.StrategyId.Should().BeNull();
        run.StrategyRevisionId.Should().BeNull();
    }

    [TestMethod]
    public void GivenStrategyMetadata_WhenCreateQueued_ThenStrategyFieldsAreSet()
    {
        var strategyId = Guid.NewGuid();

        var run = BacktestRun.CreateQueued(
            symbol: "BTC-USD",
            intervalsJson: "[\"15m\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 10000m,
            strategyId: strategyId,
            strategyRevisionId: 3);

        run.StrategyId.Should().Be(strategyId);
        run.StrategyRevisionId.Should().Be(3);
    }

    [TestMethod]
    public void GivenValidInputs_WhenCreateQueued_ThenStatusIsQueued()
    {
        var run = BacktestRun.CreateQueued(
            symbol: "ETH-USD",
            intervalsJson: "[\"1h\"]",
            startDateUtc: 1000,
            endDateUtc: 2000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 5000m);

        run.Status.Should().Be(BacktestStatus.Queued);
        run.Id.Should().NotBeEmpty();
    }
}