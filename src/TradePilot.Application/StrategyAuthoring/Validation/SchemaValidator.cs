using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public sealed class SchemaValidator
{
    public void Validate(StrategyConfig config, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(result);

        if (config.SchemaVersion < 1)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "schemaVersion",
                Code = "SCHEMA_VERSION_REQUIRED",
                Message = "Schema version must be >= 1.",
            });
        }

        if (string.IsNullOrWhiteSpace(config.StrategyName))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "strategyName",
                Code = "STRATEGY_NAME_REQUIRED",
                Message = "Strategy name is required.",
            });
        }
        else if (config.StrategyName.Length > 100)
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "strategyName",
                Code = "STRATEGY_NAME_TOO_LONG",
                Message = "Strategy name must be 100 characters or fewer.",
            });
        }

        if (string.IsNullOrWhiteSpace(config.Exchange))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "exchange",
                Code = "EXCHANGE_REQUIRED",
                Message = "Exchange is required.",
            });
        }

        if (string.IsNullOrWhiteSpace(config.Market))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "market",
                Code = "MARKET_REQUIRED",
                Message = "Market is required.",
            });
        }

        if (string.IsNullOrWhiteSpace(config.Timeframe))
        {
            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "timeframe",
                Code = "TIMEFRAME_REQUIRED",
                Message = "Timeframe is required.",
            });
        }

        ValidateEntryConditionSchema(config.EntryConditions, result);
    }

    private static void ValidateEntryConditionSchema(IReadOnlyList<EntryConditionConfig>? conditions, ValidationResult result)
    {
        if (conditions is null)
        {
            return;
        }

        for (var index = 0; index < conditions.Count; index++)
        {
            if (conditions[index].Type != EntryConditionType.Unknown)
            {
                continue;
            }

            result.Add(new ValidationError
            {
                Severity = ValidationSeverity.Error,
                FieldPath = $"entryConditions[{index}].type",
                Code = "ENTRY_CONDITION_TYPE_UNKNOWN",
                Message = "Entry condition type is not recognized.",
            });
        }
    }
}