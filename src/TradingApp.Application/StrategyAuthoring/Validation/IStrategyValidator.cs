using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public interface IStrategyValidator
{
    ValidationResult Validate(StrategyConfig config);
}