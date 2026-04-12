namespace TradingApp.Application.Trading.Services;

internal static class LeverageCalculator
{
    public const int FallbackMaxLeverage = 20;
    public const decimal FallbackMaintenanceMarginRate = 0.025m;

    public static int CalculateLeverage(decimal stopLossPercent, int maxLeverage)
    {
        if (maxLeverage <= 0)
        {
            maxLeverage = FallbackMaxLeverage;
        }

        var maintenanceMarginRate = DeriveMaintenanceMarginRate(maxLeverage);
        var denominator = stopLossPercent / 100m + maintenanceMarginRate;

        if (denominator <= 0m)
        {
            return maxLeverage;
        }

        var rawLeverage = 1m / denominator;
        return Math.Clamp((int)Math.Floor(rawLeverage), 1, maxLeverage);
    }

    public static decimal DeriveMaintenanceMarginRate(int maxLeverage)
    {
        return maxLeverage > 0
            ? 0.5m / maxLeverage
            : FallbackMaintenanceMarginRate;
    }
}