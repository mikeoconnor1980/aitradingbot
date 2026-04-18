using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Validation;

namespace TradePilot.Application.Tests.StrategyAuthoring.Validation;

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
    public void GivenRiskBasedWithNullRiskPercent_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = null,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RISK_PER_TRADE_REQUIRED");
    }

    [TestMethod]
    public void GivenRiskBasedWithZeroRiskPercent_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 0m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RISK_PER_TRADE_REQUIRED");
    }

    [TestMethod]
    public void GivenRiskBasedWithRiskPercentOver100_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 101m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RISK_PER_TRADE_INVALID");
    }

    [TestMethod]
    public void GivenRiskBasedWithRiskPercentOver5_WhenValidated_ThenWarning()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 8m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Warnings.Should().Contain(error => error.Code == "RISK_PER_TRADE_HIGH");
    }

    [TestMethod]
    public void GivenRiskBasedWithValidRiskPercent_WhenValidated_ThenNoRiskErrors()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error =>
            error.FieldPath.StartsWith("risk.riskPerTradePercent", StringComparison.Ordinal));
        result.Warnings.Should().NotContain(error =>
            error.FieldPath.StartsWith("risk.riskPerTradePercent", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GivenPercentWalletWithAutoLeverage_WhenValidated_ThenWarningReturned()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5m,
                RiskPerTradePercent = 1m,
                AutoLeverage = true,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Warnings.Should().Contain(error => error.Code == "AUTO_LEVERAGE_IGNORED");
    }

    [TestMethod]
    public void GivenAutoLeverageWithoutRiskPercent_WhenValidated_ThenErrorReturned()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = null,
                AutoLeverage = true,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "RISK_PERCENT_REQUIRED_FOR_AUTO_LEVERAGE");
    }

    [TestMethod]
    public void GivenRiskBasedWithZeroPositionSizeValue_WhenValidated_ThenNoPositionSizeError()
    {
        var config = new StrategyConfig
        {
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.RiskBased,
                RiskPerTradePercent = 1.0m,
                PositionSizeValue = 0m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "POSITION_SIZE_INVALID");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitNegative_WhenValidated_ThenSpecificErrorReturned()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = -1m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().ContainSingle(error => error.Code == "TP_R_MULTIPLE_NEGATIVE");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitBetweenZeroAndOne_WhenValidated_ThenWarningReturned()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 0.5m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Warnings.Should().ContainSingle(error => error.Code == "TP_R_MULTIPLE_SUB_ONE");
    }

    [TestMethod]
    public void GivenRMultipleTakeProfitAtOrAboveOne_WhenValidated_ThenNoRMultipleIssuesReturned()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.RMultiple,
                    Value = 2m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code == "TP_R_MULTIPLE_NEGATIVE");
        result.Warnings.Should().NotContain(error => error.Code == "TP_R_MULTIPLE_SUB_ONE");
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

    [TestMethod]
    public void GivenMultipleMacdConditions_WhenValidated_ThenMacdMaxCountErrorReturned()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                CreateMacdCondition(),
                CreateMacdCondition(),
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "MACD_MAX_COUNT");
    }

    [TestMethod]
    public void GivenMacdPeriodsOutsideAllowedRanges_WhenValidated_ThenRangeErrorsReturned()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                CreateMacdCondition(fastPeriod: 1, slowPeriod: 201, signalPeriod: 51),
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "MACD_FAST_PERIOD_RANGE");
        result.Errors.Should().Contain(error => error.Code == "MACD_SLOW_PERIOD_RANGE");
        result.Errors.Should().Contain(error => error.Code == "MACD_SIGNAL_PERIOD_RANGE");
    }

    [TestMethod]
    public void GivenMacdFastPeriodGreaterThanOrEqualToSlowPeriod_WhenValidated_ThenFastSlowErrorReturned()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                CreateMacdCondition(fastPeriod: 26, slowPeriod: 26),
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "MACD_FAST_SLOW_INVALID");
    }

    [TestMethod]
    public void GivenMacdPeriodsNotPositive_WhenValidated_ThenPeriodsInvalidErrorReturned()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                CreateMacdCondition(fastPeriod: 0, slowPeriod: 26, signalPeriod: 9),
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "MACD_PERIODS_INVALID");
    }

    [TestMethod]
    public void GivenValidMacdCondition_WhenValidated_ThenNoMacdErrorsReturned()
    {
        var config = new StrategyConfig
        {
            EntryConditions =
            [
                CreateMacdCondition(fastPeriod: 12, slowPeriod: 26, signalPeriod: 9),
            ],
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code.StartsWith("MACD_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GivenAtrInitialStopLossWithNullMultiplier_WhenValidated_ThenReturnsError()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = null,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().ContainSingle(error =>
            error.Code == "SL_ATR_MULTIPLIER_REQUIRED"
            && error.FieldPath == "exit.stopLoss.atrMultiplier");
    }

    [TestMethod]
    public void GivenAtrInitialStopLossWithValidMultiplier_WhenValidated_ThenNoError()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2.0m,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error =>
            error.FieldPath == "exit.stopLoss.atrMultiplier"
            && error.Code == "SL_ATR_MULTIPLIER_REQUIRED");
    }

    [TestMethod]
    public void GivenAtrStopLossWithNegativePeriod_WhenValidated_ThenReturnsError()
    {
        var config = new StrategyConfig
        {
            Exit = new ExitConfig
            {
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.AtrInitial,
                    AtrMultiplier = 2.0m,
                    AtrPeriod = -1,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().ContainSingle(error =>
            error.Code == "SL_ATR_PERIOD_INVALID"
            && error.FieldPath == "exit.stopLoss.atrPeriod");
    }

    [TestMethod]
    public void GivenDcaWithAllocationsNotTotalingOneHundred_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            AssetType = AssetType.Spot,
            Direction = Direction.Long,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
            Dca = new DcaConfig
            {
                TimeOfDayUtc = "09:30",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 60m },
                    new DcaAllocation { Market = "ETH-USD", WeightPercent = 30m },
                ],
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "DCA_ALLOCATION_WEIGHTS_MUST_TOTAL_100");
    }

    [TestMethod]
    public void GivenDcaWithInvalidFearGreedRange_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            AssetType = AssetType.Spot,
            Direction = Direction.Long,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
            Dca = new DcaConfig
            {
                TimeOfDayUtc = "09:30",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 100m },
                ],
                GateConditions = new DcaGateConfig
                {
                    MinFearGreedIndex = 60,
                    MaxFearGreedIndex = 40,
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "DCA_FEAR_GREED_RANGE_INVALID");
    }

    [TestMethod]
    public void GivenDcaWithMoreThanFiveScalingBands_WhenValidated_ThenError()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            AssetType = AssetType.Spot,
            Direction = Direction.Long,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
            Dca = new DcaConfig
            {
                TimeOfDayUtc = "09:30",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 100m },
                ],
                ScalingBands =
                [
                    new DcaScalingBand { PriceUpperUsd = 10m, ScalingPercent = 10m },
                    new DcaScalingBand { PriceLowerUsd = 10m, PriceUpperUsd = 20m, ScalingPercent = 10m },
                    new DcaScalingBand { PriceLowerUsd = 20m, PriceUpperUsd = 30m, ScalingPercent = 10m },
                    new DcaScalingBand { PriceLowerUsd = 30m, PriceUpperUsd = 40m, ScalingPercent = 10m },
                    new DcaScalingBand { PriceLowerUsd = 40m, PriceUpperUsd = 50m, ScalingPercent = 10m },
                    new DcaScalingBand { PriceLowerUsd = 50m, ScalingPercent = 10m },
                ],
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "DCA_SCALING_BANDS_LIMIT_EXCEEDED");
    }

    [TestMethod]
    public void GivenDcaWithValidConfig_WhenValidated_ThenNoDcaErrorsReturned()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            AssetType = AssetType.Spot,
            Direction = Direction.Long,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
            Dca = new DcaConfig
            {
                Interval = DcaInterval.Weekly,
                DayOfWeek = 1,
                TimeOfDayUtc = "09:30",
                BaseAmountUsd = 100m,
                BudgetCapUsd = 500m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 60m },
                    new DcaAllocation { Market = "ETH-USD", WeightPercent = 40m },
                ],
                GateConditions = new DcaGateConfig
                {
                    MaxPriceUsd = 120_000m,
                    MaxFearGreedIndex = 45,
                },
                ScalingBands =
                [
                    new DcaScalingBand { PriceUpperUsd = 60_000m, ScalingPercent = 20m },
                    new DcaScalingBand { PriceLowerUsd = 60_000m, ScalingPercent = -20m },
                ],
                ProfitTaking = new DcaProfitTakingConfig
                {
                    Tiers =
                    [
                        new DcaProfitTier { TargetMultiple = 2m, SellPercent = 25m },
                        new DcaProfitTier { TargetMultiple = 3m, SellPercent = 25m },
                    ],
                },
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().NotContain(error => error.Code.StartsWith("DCA_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GivenFiveMinuteDcaWithOffBoundaryTime_WhenValidated_ThenAlignmentErrorReturned()
    {
        var config = new StrategyConfig
        {
            StrategyMode = StrategyMode.Dca,
            AssetType = AssetType.Spot,
            Direction = Direction.Long,
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 1m,
                MaxOpenTrades = 1,
            },
            Dca = new DcaConfig
            {
                Interval = DcaInterval.FiveMinutes,
                TimeOfDayUtc = "09:32",
                BaseAmountUsd = 100m,
                Allocations =
                [
                    new DcaAllocation { Market = "BTC-USD", WeightPercent = 100m },
                ],
            },
        };
        var result = new ValidationResult();

        _sut.Validate(config, result);

        result.Errors.Should().Contain(error => error.Code == "DCA_FIVE_MINUTE_ALIGNMENT_REQUIRED");
    }

    private static EntryConditionConfig CreateMacdCondition(
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        return new EntryConditionConfig
        {
            Type = EntryConditionType.Macd,
            Params = new MacdParams
            {
                FastPeriod = fastPeriod,
                SlowPeriod = slowPeriod,
                SignalPeriod = signalPeriod,
                Operator = "cross_above_signal",
            },
        };
    }
}