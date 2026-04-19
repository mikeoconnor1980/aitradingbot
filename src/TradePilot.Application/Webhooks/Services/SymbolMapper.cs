namespace TradePilot.Application.Webhooks.Services;

public static class SymbolMapper
{
    private static readonly string[] QuoteSuffixes = ["USDT", "USD", "USDC", ".P", "-PERP", "PERP"];

    public static string ResolveAsset(string? ticker, string? defaultAsset)
    {
        if (!string.IsNullOrWhiteSpace(ticker))
        {
            return NormalizeTicker(ticker);
        }

        if (!string.IsNullOrWhiteSpace(defaultAsset))
        {
            return NormalizeTicker(defaultAsset);
        }

        throw new ArgumentException("Ticker or default asset is required.");
    }

    public static string NormalizeTicker(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        normalized = normalized.Replace("/", string.Empty, StringComparison.Ordinal);
        normalized = normalized.Replace("-", string.Empty, StringComparison.Ordinal);
        normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);

        var trimmed = true;
        while (trimmed)
        {
            trimmed = false;
            foreach (var suffix in QuoteSuffixes)
            {
                var comparisonSuffix = suffix.Replace(".", string.Empty, StringComparison.Ordinal);
                if (normalized.EndsWith(comparisonSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[..^comparisonSuffix.Length];
                    trimmed = true;
                    break;
                }
            }
        }

        return normalized;
    }
}