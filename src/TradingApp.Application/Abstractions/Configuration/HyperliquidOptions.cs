using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

public sealed class HyperliquidOptions
{
    public const string SectionName = "Hyperliquid";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.hyperliquid-testnet.xyz";

    [Required]
    public string WsBaseUrl { get; set; } = "wss://api.hyperliquid-testnet.xyz/ws";

    [Required]
    public string Network { get; set; } = "testnet";
}
