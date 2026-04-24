using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class HyperliquidOptions
{
    public const string SectionName = "Hyperliquid";

    private const string MainnetBaseUrl = "https://api.hyperliquid.xyz";
    private const string TestnetBaseUrl = "https://api.hyperliquid-testnet.xyz";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.hyperliquid-testnet.xyz";

    [Required]
    public string WsBaseUrl { get; set; } = "wss://api.hyperliquid-testnet.xyz/ws";

    [Required]
    public string Network { get; set; } = "testnet";

    /// <summary>
    /// Maximum slippage tolerance for market orders in basis points (bps).
    /// 100 bps = 1%. Default is 500 bps (5%).
    /// </summary>
    [Range(1, 2000)]
    public int MarketOrderSlippageBps { get; set; } = 500;

    public static string GetBaseUrlForNetwork(string network) =>
        network.Equals("mainnet", StringComparison.OrdinalIgnoreCase) ? MainnetBaseUrl : TestnetBaseUrl;
}
