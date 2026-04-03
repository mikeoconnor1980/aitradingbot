using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Validation;

namespace TradingApp.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class CrossFieldValidatorTests
{
    private readonly CrossFieldValidator _sut = new();

    [TestMethod]
    public void GivenGridModeWithNullGrid_WhenValidated_ThenError()
    {
        var config = new StrategyConfig { StrategyMode = StrategyMode.Grid, Grid = null };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error =>
            error.Code == "GRID_REQUIRED_FOR_GRID_MODE"
            && error.Message.Contains("Grid configuration required"));
    }

    [TestMethod]
    public void GivenSignalModeWithNoEntryConditions_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            EntryConditions = null,
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error =>
            error.Code == "ENTRY_CONDITIONS_REQUIRED_FOR_SIGNAL_MODE"
            && error.Message.Contains("At least one entry condition required"));
    }

    [TestMethod]
    public void GivenEnabledTrendFilter_WhenValidated_ThenLegacyInfoMessageIsNotEmitted()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            Grid = new GridConfig { Levels = 5, Spacing = 0.5m },
            TrendFilter = new TrendFilterConfig { Enabled = true, FastPeriod = 50, SlowPeriod = 200 },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.InfoMessages.Should().NotContain(error => error.Code == "TREND_FILTER_NOT_EVALUATED");
    }

    [TestMethod]
    public void GivenSignalModeWithValidFields_WhenValidated_ThenNoSignalModeInfoMessage()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Signal,
            EntryLogic = TradingApp.Application.StrategyAuthoring.Models.EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Type = EntryConditionType.Rsi,
                    Enabled = true,
                },
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.InfoMessages.Should().NotContain(error => error.Code == "SIGNAL_MODE_NOT_SUPPORTED");
    }
}