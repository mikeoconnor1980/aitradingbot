using TradePilot.Application.Abstractions.Exceptions;

namespace TradePilot.Application.MarketData;

internal static class MarketTimeframe
{
    /// <summary>
    /// Maps a supported exchange candle timeframe to its fixed duration.
    /// </summary>
    public static long GetDurationMilliseconds(string timeframe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeframe);

        var trimmed = timeframe.Trim();

        return trimmed.ToLowerInvariant() switch
        {
            "1m" when trimmed == "1M" => 2_592_000_000L,
            "1m" => 60_000L,
            "3m" => 180_000L,
            "5m" => 300_000L,
            "15m" => 900_000L,
            "30m" => 1_800_000L,
            "1h" => 3_600_000L,
            "2h" => 7_200_000L,
            "4h" => 14_400_000L,
            "8h" => 28_800_000L,
            "12h" => 43_200_000L,
            "1d" => 86_400_000L,
            "1w" => 604_800_000L,
            var unsupported => throw new DomainException(
                $"Invalid timeframe '{unsupported}'. Supported: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 8h, 12h, 1d, 1w, 1M"),
        };
    }
}
