using TradingApp.Domain.Entities;

namespace TradingApp.Domain.Tests.Entities;

[TestClass]
public sealed class StrategyTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var strategy = Strategy.Create("user-1", "BTC Grid Long", "GridStrategy", "{\"grid\":{}}");

        strategy.Id.Should().NotBeEmpty();
        strategy.UserId.Should().Be("user-1");
        strategy.Name.Should().Be("BTC Grid Long");
        strategy.StrategyType.Should().Be("GridStrategy");
        strategy.ConfigJson.Should().Be("{\"grid\":{}}");
        strategy.Version.Should().Be(1);
        strategy.IsActive.Should().BeTrue();
        strategy.CreatedAtUtc.Should().BeGreaterThan(0);
        strategy.UpdatedAtUtc.Should().Be(strategy.CreatedAtUtc);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidUserId_WhenCreate_ThenThrowsArgumentException(string? userId)
    {
        var act = () => Strategy.Create(userId!, "name", "type", "{}");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidName_WhenCreate_ThenThrowsArgumentException(string? name)
    {
        var act = () => Strategy.Create("user-1", name!, "type", "{}");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidStrategyType_WhenCreate_ThenThrowsArgumentException(string? strategyType)
    {
        var act = () => Strategy.Create("user-1", "name", strategyType!, "{}");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void GivenInvalidConfigJson_WhenCreate_ThenThrowsArgumentException(string? configJson)
    {
        var act = () => Strategy.Create("user-1", "name", "type", configJson!);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void GivenStrategy_WhenUpdate_ThenNameAndConfigUpdatedAndVersionIncremented()
    {
        var strategy = Strategy.Create("user-1", "Old Name", "GridStrategy", "{\"old\":true}");
        var originalVersion = strategy.Version;
        var originalUpdatedAt = strategy.UpdatedAtUtc;

        strategy.Update("New Name", "{\"new\":true}");

        strategy.Name.Should().Be("New Name");
        strategy.ConfigJson.Should().Be("{\"new\":true}");
        strategy.Version.Should().Be(originalVersion + 1);
        strategy.UpdatedAtUtc.Should().BeGreaterThanOrEqualTo(originalUpdatedAt);
    }

    [TestMethod]
    public void GivenStrategy_WhenSoftDelete_ThenIsActiveFalse()
    {
        var strategy = Strategy.Create("user-1", "Test", "GridStrategy", "{}");

        strategy.SoftDelete();

        strategy.IsActive.Should().BeFalse();
    }
}