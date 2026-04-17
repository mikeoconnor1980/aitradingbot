using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Validation;

namespace TradePilot.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class CompositeStrategyValidatorTests
{
    private readonly CompositeStrategyValidator _sut = new(new SchemaValidator(), new BusinessRuleValidator(), new CrossFieldValidator());

    [TestMethod]
    public void GivenValidGridConfig_WhenValidated_ThenIsValid()
    {
        var config = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = "BTC Grid",
            Market = "BTC-USD",
            Grid = new GridConfig { Levels = 10, Spacing = 0.5m },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 6m },
            },
            Risk = new RiskConfig { PositionSizeValue = 5m, Leverage = 1m, MaxOpenTrades = 1 },
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void GivenInvalidConfig_WhenValidated_ThenCollectsAllLevelErrors()
    {
        var config = new StrategyConfig
        {
            SchemaVersion = 0,
            StrategyMode = StrategyMode.Grid,
            StrategyName = string.Empty,
            Grid = new GridConfig { Levels = 0 },
            Risk = new RiskConfig { Leverage = 0 },
        };

        var result = _sut.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.FieldPath == "schemaVersion");
        result.Errors.Should().Contain(error => error.FieldPath == "strategyName");
        result.Errors.Should().Contain(error => error.FieldPath == "grid.levels");
        result.Errors.Should().Contain(error => error.FieldPath == "risk.leverage");
    }
}