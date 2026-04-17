using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public sealed class CompositeStrategyValidator : IStrategyValidator
{
    private readonly SchemaValidator _schemaValidator;
    private readonly BusinessRuleValidator _businessRuleValidator;
    private readonly CrossFieldValidator _crossFieldValidator;

    public CompositeStrategyValidator(
        SchemaValidator schemaValidator,
        BusinessRuleValidator businessRuleValidator,
        CrossFieldValidator crossFieldValidator)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _businessRuleValidator = businessRuleValidator ?? throw new ArgumentNullException(nameof(businessRuleValidator));
        _crossFieldValidator = crossFieldValidator ?? throw new ArgumentNullException(nameof(crossFieldValidator));
    }

    public ValidationResult Validate(StrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var result = new ValidationResult();
        _schemaValidator.Validate(config, result);
        _businessRuleValidator.Validate(config, result);
        _crossFieldValidator.Validate(config, result);

        return result;
    }
}