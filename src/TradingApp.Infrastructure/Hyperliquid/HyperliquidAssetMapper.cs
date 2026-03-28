using TradingApp.Application.Abstractions.Exceptions;

namespace TradingApp.Infrastructure.Hyperliquid;

public static class HyperliquidAssetMapper
{
    private static readonly Dictionary<string, string> DisplayToCoin = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC-PERP"] = "BTC",
        ["ETH-PERP"] = "ETH",
        ["SOL-PERP"] = "SOL",
        ["DOGE-PERP"] = "DOGE",
        ["AVAX-PERP"] = "AVAX",
        ["ARB-PERP"] = "ARB",
        ["LINK-PERP"] = "LINK",
        ["OP-PERP"] = "OP",
    };

    private static readonly Dictionary<string, string> CoinToDisplay =
        DisplayToCoin.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, long> TimeframeToIntervalMs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["5m"] = 5L * 60L * 1000L,
        ["15m"] = 15L * 60L * 1000L,
        ["1h"] = 60L * 60L * 1000L,
        ["4h"] = 4L * 60L * 60L * 1000L,
    };

    public static string ToCoin(string displayName)
    {
        if (displayName.EndsWith("-PERP", StringComparison.OrdinalIgnoreCase))
        {
            return displayName[..^5];
        }

        return displayName;
    }

    public static bool IsValidTimeframe(string timeframe)
    {
        return TimeframeToIntervalMs.ContainsKey(timeframe);
    }

    public static bool IsValidCoin(string coin)
    {
        return CoinToDisplay.ContainsKey(coin);
    }

    public static string ToDisplayName(string coin)
    {
        return CoinToDisplay.TryGetValue(coin, out var displayName)
            ? displayName
            : $"{coin}-PERP";
    }

    public static long GetIntervalMs(string timeframe)
    {
        return TimeframeToIntervalMs.TryGetValue(timeframe, out var ms)
            ? ms
            : throw new DomainException($"Invalid timeframe '{timeframe}'. Supported: {string.Join(", ", TimeframeToIntervalMs.Keys)}");
    }
}