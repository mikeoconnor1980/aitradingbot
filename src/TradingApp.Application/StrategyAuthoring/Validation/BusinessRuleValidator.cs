using System.Linq;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class BusinessRuleValidator
{
    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        ValidateGrid(config.Grid, result);
        ValidateExit(config.Exit, result);
        ValidateRisk(config.Risk, result);
        ValidateEntryConditions(config.EntryConditions, result);
        ValidateTrendFilter(config.TrendFilter, result);
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
}