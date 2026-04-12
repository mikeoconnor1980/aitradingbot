using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class CrossFieldValidator
{
    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        ValidateStrategyModeConsistency(config, result);
        ValidateRiskBasedRequiresStopLoss(config, result);
    }

    private static void ValidateStrategyModeConsistency(StrategyConfig config, ValidationResult result)
    {
        if (config.StrategyMode == StrategyMode.Grid && config.Grid is null)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "grid",
                Code = "GRID_REQUIRED_FOR_GRID_MODE",
                Message = "Grid configuration required for grid mode.",
            });
        }

        if (config.StrategyMode == StrategyMode.Signal)
        {
            if (config.EntryConditions is null || config.EntryConditions.Count == 0)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "entryConditions",
                    Code = "ENTRY_CONDITIONS_REQUIRED_FOR_SIGNAL_MODE",
                    Message = "At least one entry condition required for signal mode.",
                });
            }

            if (config.EntryLogic is null)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "entryLogic",
                    Code = "ENTRY_LOGIC_REQUIRED_FOR_SIGNAL_MODE",
                    Message = "Entry logic is required for signal mode.",
                });
            }
        }
    }

    private static void ValidateRiskBasedRequiresStopLoss(StrategyConfig config, ValidationResult result)
    {
        if (config.Risk.PositionSizeType != PositionSizeType.RiskBased)
        {
            return;
        }

        if (!config.Exit.StopLoss.Enabled)
        {
            if (config.StrategyMode == StrategyMode.Grid
                && config.Grid is not null
                && config.Grid.BreakdownThreshold > 0m)
            {
                return;
            }

            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exit.stopLoss",
                Code = "RISK_BASED_REQUIRES_STOP_LOSS",
                Message = "Risk-based sizing requires a stop-loss to be configured. Enable a stop-loss or use a different sizing mode.",
            });
        }
    }
}