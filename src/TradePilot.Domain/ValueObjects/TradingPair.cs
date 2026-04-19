using TradePilot.Domain.Enums;

namespace TradePilot.Domain.ValueObjects;

public sealed record TradingPair
{
    private TradingPair(string baseAsset, string quote, AssetType productType)
    {
        Base = baseAsset;
        Quote = quote;
        ProductType = productType;
    }

    public string Base { get; }

    public string Quote { get; }

    public AssetType ProductType { get; }

    public string Canonical => $"{Base}/{Quote}:{ProductType.ToString().ToUpperInvariant()}";

    public static TradingPair Create(string baseAsset, string quote, AssetType productType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(quote);

        var normalizedBase = baseAsset.Trim().ToUpperInvariant();
        var normalizedQuote = NormalizeQuote(quote);

        if (!string.Equals(normalizedQuote, "USD", StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(quote), quote, "TradingPair only supports USD as the canonical quote currency.");
        }

        return new TradingPair(normalizedBase, normalizedQuote, productType);
    }

    public static TradingPair Parse(string canonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonical);

        var parts = canonical.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid trading pair canonical format: '{canonical}'.");
        }

        var market = parts[0].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (market.Length != 2)
        {
            throw new FormatException($"Invalid trading pair market segment: '{parts[0]}'.");
        }

        if (!Enum.TryParse<AssetType>(parts[1], ignoreCase: true, out var productType))
        {
            throw new FormatException($"Invalid trading pair product type: '{parts[1]}'.");
        }

        return Create(market[0], market[1], productType);
    }

    public override string ToString()
    {
        return Canonical;
    }

    private static string NormalizeQuote(string quote)
    {
        return quote.Trim().ToUpperInvariant() switch
        {
            "USD" => "USD",
            "USDT" => "USD",
            "USDC" => "USD",
            var other => other,
        };
    }
}