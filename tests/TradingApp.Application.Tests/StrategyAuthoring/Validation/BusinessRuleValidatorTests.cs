using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Validation;

namespace TradingApp.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class BusinessRuleValidatorTests
{
    private readonly BusinessRuleValidator _sut = new();

    [TestMethod]
    public void GivenGridLevelsZero_WhenValidated_ThenErrorOnGridLevels()
    {
        var config = new StrategyConfig { Grid = new GridConfig { Levels = 0, Spacing = 0.5m } };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.FieldPath == "grid.levels");
    }

    [TestMethod]
    public void GivenLeverageZero_WhenValidated_ThenErrorOnLeverage()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig { Leverage = 0, PositionSizeValue = 5m, MaxOpenTrades = 1 },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error =>
            error.FieldPath == "risk.leverage"
            && error.Message.Contains("greater than or equal to 1"));
    }

    [TestMethod]
    public void GivenRsiValueOverHundred_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Type = EntryConditionType.Rsi,
                    Params = new RsiParams { Period = 14, Value = 101 },
                },
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RSI_VALUE_INVALID");
    }

    [TestMethod]
    public void GivenPriceAboveEmaWithPeriodZero_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 0,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "TREND_PERIOD_INVALID");
    }

    [TestMethod]
    public void GivenPriceAboveEmaWithValidPeriod_WhenValidated_ThenNoTrendFilterErrors()
    {
        var config = new StrategyConfig
        {
            TrendFilter = new TrendFilterConfig
            {
                Enabled = true,
                Type = TrendFilterType.PriceAboveEma,
                Period = 200,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error =>
            error.Code == "TREND_FAST_PERIOD_INVALID"
            || error.Code == "TREND_SLOW_PERIOD_INVALID"
            || error.Code == "TREND_PERIOD_INVALID");
    }

    [TestMethod]
    public void GivenPriceVsEmaWithNearOperatorAndNoDistanceValue_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Type = EntryConditionType.PriceVsEma,
                    Params = new PriceVsEmaParams
                    {
                        Period = 50,
                        Operator = "near",
                        DistanceValue = null,
                    },
                },
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "DISTANCE_VALUE_INVALID");
    }
}