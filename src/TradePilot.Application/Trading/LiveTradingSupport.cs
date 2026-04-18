using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Trading;

public static class LiveTradingSupport
{
    public static bool TryValidate(StrategyConfig config, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.StrategyMode == StrategyMode.Dca)
        {
            reason = "Live DCA spot execution is not implemented yet. Use backtesting for DCA strategies.";
            return false;
        }

        reason = null;
        return true;
    }
}