using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class BinanceTradingOptions
{
    public const string SectionName = "BinanceTrading";

    [Required]
    public string BaseUrl { get; set; } = "https://fapi.binance.com";

    [Range(1000, 60000)]
    public int RecvWindowMs { get; set; } = 5000;
}