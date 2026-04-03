using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class CrossFieldValidator
{
    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        ValidateStrategyModeConsistency(config, result);
        EmitV1InfoMessages(config, result);
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

    private static void EmitV1InfoMessages(StrategyConfig config, ValidationResult result)
    {
        if (config.TrendFilter is not null && config.TrendFilter.Enabled)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Info,
                FieldPath = "trendFilter",
                Code = "TREND_FILTER_NOT_EVALUATED",
                Message = "Trend filter not yet evaluated.",
            });
        }
    }
}