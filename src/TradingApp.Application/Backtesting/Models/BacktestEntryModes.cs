namespace TradingApp.Application.Backtesting.Models;

public static class BacktestEntryModes
{
    public const string AutoFromSignalCandle = "AutoFromSignalCandle";
    public const string InitialMarketThenGrid = "InitialMarketThenGrid";
    public const string WaitForLimitPrice = "WaitForLimitPrice";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, AutoFromSignalCandle, StringComparison.Ordinal) ||
               string.Equals(value, InitialMarketThenGrid, StringComparison.Ordinal) ||
               string.Equals(value, WaitForLimitPrice, StringComparison.Ordinal);
    }
}