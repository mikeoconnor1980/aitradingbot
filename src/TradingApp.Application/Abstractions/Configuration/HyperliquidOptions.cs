using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

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

    public static string GetBaseUrlForNetwork(string network) =>
        network.Equals("mainnet", StringComparison.OrdinalIgnoreCase) ? MainnetBaseUrl : TestnetBaseUrl;
}
