using Microsoft.Extensions.Options;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Validation;

public sealed class RiskLimitsConfigValidator : IValidateOptions<RiskLimitsConfig>
{
    public ValidateOptionsResult Validate(string? name, RiskLimitsConfig options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var tiers = options.DrawdownTiers;
        if (tiers.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].ThresholdPercent <= 0m)
            {
                return ValidateOptionsResult.Fail($"DrawdownTiers[{i}].ThresholdPercent must be greater than 0.");
            }

            if (tiers[i].ScalingFactor < 0m || tiers[i].ScalingFactor > 1m)
            {
                return ValidateOptionsResult.Fail($"DrawdownTiers[{i}].ScalingFactor must be between 0.0 and 1.0.");
            }

            if (i > 0 && tiers[i].ThresholdPercent <= tiers[i - 1].ThresholdPercent)
            {
                return ValidateOptionsResult.Fail("DrawdownTiers must be in ascending ThresholdPercent order.");
            }

            if (i > 0 && tiers[i].ScalingFactor >= tiers[i - 1].ScalingFactor)
            {
                return ValidateOptionsResult.Fail("DrawdownTiers must be in descending ScalingFactor order.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}