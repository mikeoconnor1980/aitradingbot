using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Tests.Entities;

[TestClass]
public sealed class StrategyRevisionTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var strategyId = Guid.NewGuid();

        var revision = StrategyRevision.Create(
            strategyId,
            1,
            "{\"grid\":{}}",
            RevisionSource.Ui,
            "Initial version");

        revision.Id.Should().NotBeEmpty();
        revision.StrategyId.Should().Be(strategyId);
        revision.RevisionNumber.Should().Be(1);
        revision.ConfigJson.Should().Be("{\"grid\":{}}");
        revision.Source.Should().Be(RevisionSource.Ui);
        revision.Label.Should().BeNull();
        revision.ChangeSummary.Should().Be("Initial version");
        revision.CreatedAtUtc.Should().BePositive();
    }

    [TestMethod]
    public void GivenLabel_WhenCreate_ThenLabelSet()
    {
        var revision = StrategyRevision.Create(
            Guid.NewGuid(),
            2,
            "{}",
            RevisionSource.Restore,
            "Restored",
            "Restored from revision 1");

        revision.Label.Should().Be("Restored from revision 1");
    }

    [TestMethod]
    public void GivenEmptyStrategyId_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => StrategyRevision.Create(Guid.Empty, 1, "{}", RevisionSource.Ui, "Initial version");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void GivenInvalidRevisionNumber_WhenCreate_ThenThrowsArgumentOutOfRangeException(int revisionNumber)
    {
        var act = () => StrategyRevision.Create(Guid.NewGuid(), revisionNumber, "{}", RevisionSource.Ui, "summary");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidConfigJson_WhenCreate_ThenThrowsArgumentException(string? configJson)
    {
        var act = () => StrategyRevision.Create(Guid.NewGuid(), 1, configJson!, RevisionSource.Ui, "summary");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidChangeSummary_WhenCreate_ThenThrowsArgumentException(string? changeSummary)
    {
        var act = () => StrategyRevision.Create(Guid.NewGuid(), 1, "{}", RevisionSource.Ui, changeSummary!);

        act.Should().Throw<ArgumentException>();
    }
}