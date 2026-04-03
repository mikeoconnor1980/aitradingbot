using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Validation;

namespace TradingApp.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class SchemaValidatorTests
{
    private readonly SchemaValidator _sut = new();

    [TestMethod]
    public void GivenEmptyStrategyName_WhenValidated_ThenErrorOnStrategyName()
    {
        var config = new StrategyConfig { StrategyName = string.Empty };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error =>
            error.FieldPath == "strategyName"
            && error.Code == "STRATEGY_NAME_REQUIRED");
    }

    [TestMethod]
    public void GivenSchemaVersionZero_WhenValidated_ThenErrorOnSchemaVersion()
    {
        var config = new StrategyConfig { SchemaVersion = 0, StrategyName = "Test", Market = "BTC-USD" };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.FieldPath == "schemaVersion");
    }

    [TestMethod]
    public void GivenStrategyNameOver100Chars_WhenValidated_ThenErrorOnStrategyName()
    {
        var config = new StrategyConfig { StrategyName = new string('A', 101), Market = "BTC-USD" };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "STRATEGY_NAME_TOO_LONG");
    }
}