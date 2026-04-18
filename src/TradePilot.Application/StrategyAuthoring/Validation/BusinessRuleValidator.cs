using System.Globalization;
using System.Linq;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public sealed class BusinessRuleValidator
{
    private static readonly HashSet<string> ValidCandlePatterns =
    [
        "bullish_engulfing",
        "bearish_engulfing",
        "bullish_rejection",
        "bearish_rejection",
        "bullish_continuation",
        "bearish_continuation",
        "bullish_rejection_or_engulfing",
        "bearish_rejection_or_engulfing",
    ];

    private static readonly HashSet<string> ValidSweepSides =
    [
        "upside",
        "downside",
    ];

    private static readonly HashSet<string> ValidStructureShiftDirections =
    [
        "bullish",
        "bearish",
    ];

    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        ValidateGrid(config.Grid, result);
        ValidateExit(config.Exit, result);
        ValidateRisk(config.Risk, result);
        ValidateEntryConditions(config.EntryConditions, result);
        ValidateTrendFilter(config.TrendFilter, result);
        ValidateDca(config, result);
    }

    private static void ValidateGrid(GridConfig? grid, ValidationResult result)
    {
        if (grid is null)
        {
            return;
        }

        if (grid.Levels <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "grid.levels",
                Code = "GRID_LEVELS_INVALID",
                Message = "Grid levels must be greater than 0.",
            });
        }

        if (grid.Spacing <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "grid.spacing",
                Code = "GRID_SPACING_INVALID",
                Message = "Grid spacing must be greater than 0.",
            });
        }
    }

    private static void ValidateExit(ExitConfig exit, ValidationResult result)
    {
        if (exit.TakeProfit.Enabled
            && exit.TakeProfit.Type != ExitRuleType.RMultiple
            && exit.TakeProfit.Value is not null
            && exit.TakeProfit.Value <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.takeProfit.value",
                Code = "TP_VALUE_INVALID",
                Message = "Take profit value must be greater than 0 when enabled.",
            });
        }

        if (exit.TakeProfit.Enabled && exit.TakeProfit.Type == ExitRuleType.RMultiple)
        {
            if (exit.TakeProfit.Value is not null && exit.TakeProfit.Value < 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "exit.takeProfit.value",
                    Code = "TP_R_MULTIPLE_NEGATIVE",
                    Message = "R-multiple take profit target must not be negative.",
                });
            }
            else if (exit.TakeProfit.Value is not null && exit.TakeProfit.Value == 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "exit.takeProfit.value",
                    Code = "TP_VALUE_INVALID",
                    Message = "Take profit value must be greater than 0 when enabled.",
                });
            }
            else if (exit.TakeProfit.Value is not null && exit.TakeProfit.Value < 1m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Warning,
                    FieldPath = "exit.takeProfit.value",
                    Code = "TP_R_MULTIPLE_SUB_ONE",
                    Message = "Sub-1R take profit - this trade relies on a high win rate to be profitable.",
                });
            }
        }

        if (exit.StopLoss.Enabled && exit.StopLoss.Value is not null && exit.StopLoss.Value <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss.value",
                Code = "SL_VALUE_INVALID",
                Message = "Stop loss value must be greater than 0 when enabled.",
            });
        }

        if (exit.StopLoss.Enabled
            && exit.StopLoss.Type == ExitRuleType.SwingLow
            && (exit.StopLoss.Lookback is null || exit.StopLoss.Lookback <= 0))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss.lookback",
                Code = "SL_LOOKBACK_REQUIRED",
                Message = "Stop loss lookback must be > 0 when type is swing_low.",
            });
        }

        if (exit.StopLoss.Enabled
            && (exit.StopLoss.Type == ExitRuleType.AtrTrailing || exit.StopLoss.Type == ExitRuleType.AtrInitial)
            && (exit.StopLoss.AtrMultiplier is null || exit.StopLoss.AtrMultiplier <= 0))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss.atrMultiplier",
                Code = "SL_ATR_MULTIPLIER_REQUIRED",
                Message = "ATR multiplier must be > 0 when type is atr_trailing or atr_initial.",
            });
        }

        if (exit.StopLoss.Enabled
            && (exit.StopLoss.Type == ExitRuleType.AtrTrailing || exit.StopLoss.Type == ExitRuleType.AtrInitial)
            && exit.StopLoss.AtrPeriod.HasValue
            && exit.StopLoss.AtrPeriod <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss.atrPeriod",
                Code = "SL_ATR_PERIOD_INVALID",
                Message = "ATR period must be > 0 when specified.",
            });
        }

        if (exit.StopLoss.Enabled
            && exit.StopLoss.Type == ExitRuleType.AtrTrailing
            && exit.StopLoss.TrailingStopWarmup.HasValue
            && exit.StopLoss.TrailingStopWarmup < 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss.trailingStopWarmup",
                Code = "SL_WARMUP_INVALID",
                Message = "Trailing stop warmup must be >= 0 when specified.",
            });
        }
    }

    private static void ValidateRisk(RiskConfig risk, ValidationResult result)
    {
        if (risk.PositionSizeType == PositionSizeType.RiskBased)
        {
            if (!risk.RiskPerTradePercent.HasValue || risk.RiskPerTradePercent.Value <= 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "risk.riskPerTradePercent",
                    Code = "RISK_PER_TRADE_REQUIRED",
                    Message = "Risk per trade percent must be greater than 0 when using risk-based sizing.",
                });
            }
            else if (risk.RiskPerTradePercent.Value > 100m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "risk.riskPerTradePercent",
                    Code = "RISK_PER_TRADE_INVALID",
                    Message = "Risk per trade percent must not exceed 100.",
                });
            }
            else if (risk.RiskPerTradePercent.Value > 5m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Warning,
                    FieldPath = "risk.riskPerTradePercent",
                    Code = "RISK_PER_TRADE_HIGH",
                    Message = "Risk per trade exceeds 5% - this is considered high risk.",
                });
            }
        }
        else if (risk.PositionSizeValue <= 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.positionSizeValue",
                Code = "POSITION_SIZE_INVALID",
                Message = "Position size value must be greater than 0.",
            });
        }

        if (risk.Leverage < 1)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.leverage",
                Code = "LEVERAGE_INVALID",
                Message = "Leverage must be greater than or equal to 1.",
            });
        }

        if (risk.AutoLeverage && risk.PositionSizeType != PositionSizeType.RiskBased)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Warning,
                FieldPath = "risk.autoLeverage",
                Code = "AUTO_LEVERAGE_IGNORED",
                Message = "Auto-leverage is only effective with RiskBased position sizing. It will be ignored.",
            });
        }

        if (risk.AutoLeverage
            && risk.PositionSizeType == PositionSizeType.RiskBased
            && risk.RiskPerTradePercent is null)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.riskPerTradePercent",
                Code = "RISK_PERCENT_REQUIRED_FOR_AUTO_LEVERAGE",
                Message = "RiskPerTradePercent is required when AutoLeverage is enabled.",
            });
        }

        if (risk.MaxOpenTrades < 1)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.maxOpenTrades",
                Code = "MAX_OPEN_TRADES_INVALID",
                Message = "Max open trades must be at least 1.",
            });
        }
    }

    private static void ValidateEntryConditions(IReadOnlyList<EntryConditionConfig>? conditions, ValidationResult result)
    {
        if (conditions is null)
        {
            return;
        }

        var macdCount = conditions.Count(condition => condition.Type == EntryConditionType.Macd);
        if (macdCount > 1)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "entryConditions",
                Code = "MACD_MAX_COUNT",
                Message = "Only one MACD condition is allowed per strategy.",
            });
        }

        for (var index = 0; index < conditions.Count; index++)
        {
            var condition = conditions[index];

            if (condition.Params is RsiParams rsi)
            {
                if (rsi.Period <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.period",
                        Code = "RSI_PERIOD_INVALID",
                        Message = "RSI period must be greater than 0.",
                    });
                }

                if (rsi.Value is < 0 or > 100)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.value",
                        Code = "RSI_VALUE_INVALID",
                        Message = "RSI value must be between 0 and 100.",
                    });
                }
            }

            if (condition.Params is PriceVsEmaParams priceVsEma)
            {
                if (priceVsEma.Period <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.period",
                        Code = "EMA_PERIOD_INVALID",
                        Message = "EMA period must be greater than 0.",
                    });
                }

                var normalizedOperator = priceVsEma.Operator.Trim().ToLowerInvariant();
                if (normalizedOperator == "near"
                    && (!priceVsEma.DistanceValue.HasValue || priceVsEma.DistanceValue.Value <= 0))
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.distanceValue",
                        Code = "DISTANCE_VALUE_INVALID",
                        Message = "Distance value must be greater than 0 when operator is 'near'.",
                    });
                }
            }

            if (condition.Params is MacdParams macd)
            {
                if (macd.FastPeriod <= 0 || macd.SlowPeriod <= 0 || macd.SignalPeriod <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params",
                        Code = "MACD_PERIODS_INVALID",
                        Message = "MACD fast, slow, and signal periods must all be greater than 0.",
                    });
                }

                if (macd.FastPeriod < 2 || macd.FastPeriod > 50)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.fastPeriod",
                        Code = "MACD_FAST_PERIOD_RANGE",
                        Message = "MACD fast period must be between 2 and 50.",
                    });
                }

                if (macd.SlowPeriod < 5 || macd.SlowPeriod > 200)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.slowPeriod",
                        Code = "MACD_SLOW_PERIOD_RANGE",
                        Message = "MACD slow period must be between 5 and 200.",
                    });
                }

                if (macd.SignalPeriod < 2 || macd.SignalPeriod > 50)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.signalPeriod",
                        Code = "MACD_SIGNAL_PERIOD_RANGE",
                        Message = "MACD signal period must be between 2 and 50.",
                    });
                }

                if (macd.FastPeriod >= macd.SlowPeriod)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.fastPeriod",
                        Code = "MACD_FAST_SLOW_INVALID",
                        Message = "MACD fast period must be less than slow period.",
                    });
                }
            }

            if (condition.Params is SupportResistanceParams sr)
            {
                if (sr.Lookback < 10 || sr.Lookback > 500)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.lookback",
                        Code = "SR_LOOKBACK_RANGE",
                        Message = "Support/resistance lookback must be between 10 and 500.",
                    });
                }

                if (sr.Strength < 1 || sr.Strength > 10)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.strength",
                        Code = "SR_STRENGTH_RANGE",
                        Message = "Support/resistance strength must be between 1 and 10.",
                    });
                }

                if (sr.Tolerance < 0 || sr.Tolerance > 10)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.tolerance",
                        Code = "SR_TOLERANCE_RANGE",
                        Message = "Support/resistance tolerance must be between 0 and 10 percent.",
                    });
                }
            }

            if (condition.Params is CandlePatternParams candlePattern
                && !ValidCandlePatterns.Contains(candlePattern.Pattern.Trim().ToLowerInvariant()))
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"entryConditions[{index}].params.pattern",
                    Code = "CANDLE_PATTERN_INVALID",
                    Message = "Candle pattern must be one of the supported derived signal patterns.",
                });
            }

            if (condition.Params is LiquiditySweepParams liquiditySweep)
            {
                if (liquiditySweep.LookbackBars is < 1 or > 200)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.lookbackBars",
                        Code = "LIQUIDITY_SWEEP_LOOKBACK_RANGE",
                        Message = "Liquidity sweep lookback bars must be between 1 and 200.",
                    });
                }

                if (liquiditySweep.PivotBars is < 1 or > 10)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.pivotBars",
                        Code = "LIQUIDITY_SWEEP_PIVOT_RANGE",
                        Message = "Liquidity sweep pivot bars must be between 1 and 10.",
                    });
                }

                if (!ValidSweepSides.Contains(liquiditySweep.Side.Trim().ToLowerInvariant()))
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.side",
                        Code = "LIQUIDITY_SWEEP_SIDE_INVALID",
                        Message = "Liquidity sweep side must be 'upside' or 'downside'.",
                    });
                }
            }

            if (condition.Params is StructureShiftParams structureShift)
            {
                if (structureShift.PivotBars is < 1 or > 10)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.pivotBars",
                        Code = "STRUCTURE_SHIFT_PIVOT_RANGE",
                        Message = "Structure shift pivot bars must be between 1 and 10.",
                    });
                }

                if (!ValidStructureShiftDirections.Contains(structureShift.Direction.Trim().ToLowerInvariant()))
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = $"entryConditions[{index}].params.direction",
                        Code = "STRUCTURE_SHIFT_DIRECTION_INVALID",
                        Message = "Structure shift direction must be 'bullish' or 'bearish'.",
                    });
                }
            }
        }
    }

    private static void ValidateTrendFilter(TrendFilterConfig? filter, ValidationResult result)
    {
        if (filter is null || !filter.Enabled)
        {
            return;
        }

        switch (filter.Type)
        {
            case TrendFilterType.EmaCross:
            case TrendFilterType.EmaSingle:
            case TrendFilterType.SmaCross:
                if (filter.FastPeriod <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "trendFilter.fastPeriod",
                        Code = "TREND_FAST_PERIOD_INVALID",
                        Message = "Trend filter fast period must be greater than 0.",
                    });
                }

                if (filter.SlowPeriod <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "trendFilter.slowPeriod",
                        Code = "TREND_SLOW_PERIOD_INVALID",
                        Message = "Trend filter slow period must be greater than 0.",
                    });
                }

                break;

            case TrendFilterType.PriceAboveEma:
                if (filter.Period is null or <= 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "trendFilter.period",
                        Code = "TREND_PERIOD_INVALID",
                        Message = "Trend filter period must be greater than 0.",
                    });
                }

                break;
        }
    }

    private static void ValidateDca(StrategyConfig config, ValidationResult result)
    {
        var dca = config.Dca;
        if (dca is null)
        {
            return;
        }

        if (dca.BaseAmountUsd <= 0m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.baseAmountUsd",
                Code = "DCA_BASE_AMOUNT_INVALID",
                Message = "DCA base amount must be greater than 0.",
            });
        }

        if (!TimeOnly.TryParseExact(dca.TimeOfDayUtc, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.timeOfDayUtc",
                Code = "DCA_TIME_OF_DAY_INVALID",
                Message = "DCA time of day must use HH:mm UTC format.",
            });
        }

        ValidateDcaIntervalFields(dca, result);
        ValidateDcaAllocations(dca, result);
        ValidateDcaGateConditions(dca.GateConditions, result);
        ValidateDcaScalingBands(dca.ScalingBands, result);
        ValidateDcaProfitTaking(dca.ProfitTaking, result);

        if (dca.BudgetCapUsd.HasValue && dca.BudgetCapUsd.Value <= 0m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.budgetCapUsd",
                Code = "DCA_BUDGET_CAP_INVALID",
                Message = "DCA budget cap must be greater than 0 when provided.",
            });
        }

        if (config.StrategyMode == StrategyMode.Dca && config.Risk.AutoLeverage)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.autoLeverage",
                Code = "DCA_AUTO_LEVERAGE_NOT_SUPPORTED",
                Message = "Auto-leverage is not supported for DCA spot accumulation.",
            });
        }

        if (config.StrategyMode == StrategyMode.Dca && config.Risk.Leverage != 1m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "risk.leverage",
                Code = "DCA_LEVERAGE_NOT_SUPPORTED",
                Message = "Leverage must remain 1 for DCA spot accumulation.",
            });
        }
    }

    private static void ValidateDcaIntervalFields(DcaConfig dca, ValidationResult result)
    {
        switch (dca.Interval)
        {
            case DcaInterval.FiveMinutes:
                if (TimeOnly.TryParseExact(dca.TimeOfDayUtc, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var scheduledTime)
                    && scheduledTime.Minute % 5 != 0)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "dca.timeOfDayUtc",
                        Code = "DCA_FIVE_MINUTE_ALIGNMENT_REQUIRED",
                        Message = "5-minute DCA schedules require a UTC time aligned to a 5-minute boundary.",
                    });
                }

                break;

            case DcaInterval.Weekly:
            case DcaInterval.Biweekly:
                if (!dca.DayOfWeek.HasValue || dca.DayOfWeek.Value is < 0 or > 6)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "dca.dayOfWeek",
                        Code = "DCA_DAY_OF_WEEK_REQUIRED",
                        Message = "Weekly and biweekly DCA schedules require a day of week between 0 and 6.",
                    });
                }

                break;

            case DcaInterval.Monthly:
                if (!dca.DayOfMonth.HasValue || dca.DayOfMonth.Value is < 1 or > 28)
                {
                    result.Add(new ValidationError
                    {
                        Severity = ValidationSeverity.Error,
                        FieldPath = "dca.dayOfMonth",
                        Code = "DCA_DAY_OF_MONTH_REQUIRED",
                        Message = "Monthly DCA schedules require a day of month between 1 and 28.",
                    });
                }

                break;
        }
    }

    private static void ValidateDcaAllocations(DcaConfig dca, ValidationResult result)
    {
        if (dca.Allocations.Count == 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.allocations",
                Code = "DCA_ALLOCATIONS_REQUIRED",
                Message = "At least one DCA allocation is required.",
            });

            return;
        }

        for (var index = 0; index < dca.Allocations.Count; index++)
        {
            var allocation = dca.Allocations[index];

            if (string.IsNullOrWhiteSpace(allocation.Market))
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.allocations[{index}].market",
                    Code = "DCA_ALLOCATION_MARKET_REQUIRED",
                    Message = "Each DCA allocation must specify a market.",
                });
            }

            if (allocation.WeightPercent <= 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.allocations[{index}].weightPercent",
                    Code = "DCA_ALLOCATION_WEIGHT_INVALID",
                    Message = "DCA allocation weights must be greater than 0.",
                });
            }
        }

        var totalWeight = dca.Allocations.Sum(allocation => allocation.WeightPercent);
        if (Math.Abs(totalWeight - 100m) > 0.0001m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.allocations",
                Code = "DCA_ALLOCATION_WEIGHTS_MUST_TOTAL_100",
                Message = "DCA allocation weights must total 100%.",
            });
        }
    }

    private static void ValidateDcaGateConditions(DcaGateConfig? gates, ValidationResult result)
    {
        if (gates is null)
        {
            return;
        }

        if (gates.MaxPriceUsd.HasValue && gates.MaxPriceUsd.Value <= 0m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.gateConditions.maxPriceUsd",
                Code = "DCA_MAX_PRICE_INVALID",
                Message = "DCA max price gate must be greater than 0 when provided.",
            });
        }

        if (gates.MinFearGreedIndex.HasValue && gates.MinFearGreedIndex.Value is < 0 or > 100)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.gateConditions.minFearGreedIndex",
                Code = "DCA_MIN_FEAR_GREED_INVALID",
                Message = "DCA minimum Fear & Greed value must be between 0 and 100.",
            });
        }

        if (gates.MaxFearGreedIndex.HasValue && gates.MaxFearGreedIndex.Value is < 0 or > 100)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.gateConditions.maxFearGreedIndex",
                Code = "DCA_MAX_FEAR_GREED_INVALID",
                Message = "DCA maximum Fear & Greed value must be between 0 and 100.",
            });
        }

        if (gates.MinFearGreedIndex.HasValue
            && gates.MaxFearGreedIndex.HasValue
            && gates.MinFearGreedIndex.Value > gates.MaxFearGreedIndex.Value)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.gateConditions",
                Code = "DCA_FEAR_GREED_RANGE_INVALID",
                Message = "DCA minimum Fear & Greed value must be less than or equal to the maximum value.",
            });
        }
    }

    private static void ValidateDcaScalingBands(IReadOnlyList<DcaScalingBand>? bands, ValidationResult result)
    {
        if (bands is null)
        {
            return;
        }

        if (bands.Count > 5)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.scalingBands",
                Code = "DCA_SCALING_BANDS_LIMIT_EXCEEDED",
                Message = "DCA supports at most 5 scaling bands.",
            });
        }

        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];

            if (band.PriceLowerUsd.HasValue && band.PriceLowerUsd.Value < 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.scalingBands[{index}].priceLowerUsd",
                    Code = "DCA_SCALING_BAND_LOWER_INVALID",
                    Message = "DCA scaling band lower price must be 0 or greater.",
                });
            }

            if (band.PriceUpperUsd.HasValue && band.PriceUpperUsd.Value <= 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.scalingBands[{index}].priceUpperUsd",
                    Code = "DCA_SCALING_BAND_UPPER_INVALID",
                    Message = "DCA scaling band upper price must be greater than 0.",
                });
            }

            if (band.PriceLowerUsd.HasValue
                && band.PriceUpperUsd.HasValue
                && band.PriceLowerUsd.Value >= band.PriceUpperUsd.Value)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.scalingBands[{index}]",
                    Code = "DCA_SCALING_BAND_RANGE_INVALID",
                    Message = "DCA scaling band lower price must be less than upper price.",
                });
            }

            if (band.ScalingPercent < -100m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.scalingBands[{index}].scalingPercent",
                    Code = "DCA_SCALING_PERCENT_INVALID",
                    Message = "DCA scaling percent must be greater than or equal to -100.",
                });
            }
        }
    }

    private static void ValidateDcaProfitTaking(DcaProfitTakingConfig? profitTaking, ValidationResult result)
    {
        if (profitTaking is null)
        {
            return;
        }

        if (profitTaking.Tiers.Count == 0)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.profitTaking.tiers",
                Code = "DCA_PROFIT_TIERS_REQUIRED",
                Message = "DCA profit taking requires at least one tier.",
            });

            return;
        }

        decimal totalSellPercent = 0m;
        for (var index = 0; index < profitTaking.Tiers.Count; index++)
        {
            var tier = profitTaking.Tiers[index];
            totalSellPercent += tier.SellPercent;

            if (tier.TargetMultiple <= 0m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.profitTaking.tiers[{index}].targetMultiple",
                    Code = "DCA_PROFIT_TARGET_INVALID",
                    Message = "DCA profit-taking target multiple must be greater than 0.",
                });
            }

            if (tier.SellPercent <= 0m || tier.SellPercent > 100m)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"dca.profitTaking.tiers[{index}].sellPercent",
                    Code = "DCA_PROFIT_SELL_PERCENT_INVALID",
                    Message = "DCA profit-taking sell percent must be greater than 0 and less than or equal to 100.",
                });
            }
        }

        if (totalSellPercent > 100m)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "dca.profitTaking.tiers",
                Code = "DCA_PROFIT_SELL_PERCENT_TOTAL_INVALID",
                Message = "DCA profit-taking sell percentages must not exceed 100 in total.",
            });
        }
    }
}