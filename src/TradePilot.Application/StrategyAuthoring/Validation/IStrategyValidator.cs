using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public interface IStrategyValidator
{
    ValidationResult Validate(StrategyConfig config);
}