using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading;

public static class LiveTradingSupport
{
    public static bool TryValidate(StrategyConfig config, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.StrategyMode != StrategyMode.Dca)
        {
            reason = null;
            return true;
        }

        if (config.AssetType != AssetType.Spot)
        {
            reason = "Live DCA requires a spot asset type.";
            return false;
        }

        if (config.Direction != Direction.Long)
        {
            reason = "Live DCA currently supports long accumulation only.";
            return false;
        }

        reason = null;
        return true;
    }
}