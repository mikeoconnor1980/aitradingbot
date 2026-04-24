using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class BinanceSpotTradingOptions
{
    public const string SectionName = "BinanceSpotTrading";

    [Required]
    public string BaseUrl { get; set; } = "https://api.binance.com";

    [Range(1000, 60000)]
    public int RecvWindowMs { get; set; } = 5000;
}
