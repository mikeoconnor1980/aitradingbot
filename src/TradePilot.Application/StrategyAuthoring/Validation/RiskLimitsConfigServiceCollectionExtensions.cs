using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Validation;

public static class RiskLimitsConfigDefaults
{
	public static void Apply(RiskLimitsConfig options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.DrawdownTiers.Count == 0)
		{
			options.DrawdownTiers = RiskLimitsConfig.DefaultDrawdownTiers.ToArray();
		}
	}
}