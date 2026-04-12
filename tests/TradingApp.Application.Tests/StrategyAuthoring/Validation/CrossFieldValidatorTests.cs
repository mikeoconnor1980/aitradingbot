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

    [TestMethod]
    public void GivenRiskBasedWithNoStopLoss_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
            },
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig { Enabled = false },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RISK_BASED_REQUIRES_STOP_LOSS");
    }

    [TestMethod]
    public void GivenRiskBasedWithStopLossEnabled_WhenValidated_ThenNoError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
            },
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 2.0m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "RISK_BASED_REQUIRES_STOP_LOSS");
    }

    [TestMethod]
    public void GivenRiskBasedGridWithBreakdownThresholdNoStopLoss_WhenValidated_ThenNoError()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Grid,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
            },
            Grid = new GridConfig
            {
                Levels = 5,
                Spacing = 0.5m,
                BreakdownThreshold = 5.0m,
            },
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig { Enabled = false },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "RISK_BASED_REQUIRES_STOP_LOSS");
    }

    [TestMethod]
    public void GivenPercentWalletWithNoStopLoss_WhenValidated_ThenNoRiskBasedError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m,
            },
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig { Enabled = false },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "RISK_BASED_REQUIRES_STOP_LOSS");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitWithNonRiskBasedSizing_WhenValidated_ThenErrorReturned()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 2m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 2m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().ContainSingle(error => error.Code == "R_MULTIPLE_TP_REQUIRES_RISK_BASED");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitWithRiskBasedAndStopLoss_WhenValidated_ThenNoRMultipleErrorReturned()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 2m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 2m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "R_MULTIPLE_TP_REQUIRES_RISK_BASED");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitWithRiskBasedAndNoStopLoss_WhenValidated_ThenStopLossErrorReturned()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 2m,
                },
                StopLoss = new ExitRuleConfig { Enabled = false },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().ContainSingle(error => error.Code == "RISK_BASED_REQUIRES_STOP_LOSS");
        result.Errors.Should().NotContain(error => error.Code == "R_MULTIPLE_TP_REQUIRES_RISK_BASED");
    }
}