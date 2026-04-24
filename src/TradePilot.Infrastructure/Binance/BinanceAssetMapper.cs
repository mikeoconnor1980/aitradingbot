using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceAssetMapper : IExchangeSymbolMapper
{
    private const string MarkPricePrefix = "mark-";
    private static readonly string[] KnownDisplaySuffixes = ["-USD", "-USDT", "-PERP"];

    private static readonly Dictionary<string, string> SymbolToFuturesSymbol = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "BTCUSDT",
        ["ETH"] = "ETHUSDT",
        ["SOL"] = "SOLUSDT",
        ["DOGE"] = "DOGEUSDT",
        ["AVAX"] = "AVAXUSDT",
        ["ARB"] = "ARBUSDT",
        ["LINK"] = "LINKUSDT",
        ["OP"] = "OPUSDT",
    };

    private static readonly Dictionary<string, long> IntervalToMs = new(StringComparer.Ordinal)
    {
        ["5m"] = 300_000L,
        ["15m"] = 900_000L,
        ["1h"] = 3_600_000L,
        ["4h"] = 14_400_000L,
        ["1d"] = 86_400_000L,
    };

    public static IReadOnlySet<string> SupportedAssets { get; } = new HashSet<string>(
        SymbolToFuturesSymbol.Keys,
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> ValidSymbols => SymbolToFuturesSymbol.Keys;

    public static IReadOnlyCollection<string> ValidIntervals => IntervalToMs.Keys;

    public Exchange Exchange => Exchange.Binance;

    public static string NormalizeSymbol(string displaySymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displaySymbol);

        var normalizedSymbol = displaySymbol.Trim();

        if (SymbolToFuturesSymbol.ContainsKey(normalizedSymbol))
        {
            return normalizedSymbol;
        }

        foreach (var suffix in KnownDisplaySuffixes)
        {
            if (!normalizedSymbol.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var strippedSymbol = normalizedSymbol[..^suffix.Length];

            if (SymbolToFuturesSymbol.ContainsKey(strippedSymbol))
            {
                return strippedSymbol;
            }
        }

        return normalizedSymbol;
    }

    public static string ToFuturesSymbol(string displaySymbol)
    {
        var normalizedSymbol = NormalizeSymbol(displaySymbol);

        if (SymbolToFuturesSymbol.TryGetValue(normalizedSymbol, out var futuresSymbol))
        {
            return futuresSymbol;
        }

        throw new DomainException(
            $"Unsupported Binance symbol: '{displaySymbol}'. Valid symbols: {string.Join(", ", SymbolToFuturesSymbol.Keys)}");
    }

    public static bool IsValidSymbol(string displaySymbol)
        => SymbolToFuturesSymbol.ContainsKey(NormalizeSymbol(displaySymbol));

    public static bool IsValidInterval(string interval)
        => IntervalToMs.ContainsKey(interval);

    public static long GetIntervalMs(string interval)
    {
        var normalizedInterval = interval.StartsWith(MarkPricePrefix, StringComparison.Ordinal)
            ? interval[MarkPricePrefix.Length..]
            : interval;

        if (IntervalToMs.TryGetValue(normalizedInterval, out var intervalMs))
        {
            return intervalMs;
        }

        throw new DomainException(
            $"Unsupported Binance interval: '{interval}'. Valid intervals: {string.Join(", ", IntervalToMs.Keys)}");
    }

    string IExchangeSymbolMapper.ToExchangeSymbol(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        if (!((IExchangeSymbolMapper)this).CanMap(pair))
        {
            throw new InvalidOperationException($"Binance cannot map trading pair '{pair.Canonical}'.");
        }

        return ToFuturesSymbol(pair.Base);
    }

    TradingPair IExchangeSymbolMapper.FromExchangeSymbol(string exchangeSymbol)
    {
        var normalized = NormalizeSymbol(exchangeSymbol);
        if (normalized.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (normalized.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        return TradingPair.Create(normalized, "USD", AssetType.Perp);
    }

    bool IExchangeSymbolMapper.CanMap(TradingPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return IsValidSymbol(pair.Base);
    }
}