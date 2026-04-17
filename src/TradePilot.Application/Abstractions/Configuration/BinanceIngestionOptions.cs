using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class BinanceIngestionOptions
{
    public const string SectionName = "BinanceIngestion";

    [Range(0, 60000)]
    public int BatchDelayMs { get; set; } = 250;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(60000, 28800000)]
    public int MaxIngestionTimeoutMs { get; set; } = 7200000;

    [Required]
    public DateTime DefaultStartDate { get; set; } = new(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Range(100, 1500)]
    public int PageSize { get; set; } = 1500;

    [Required]
    public string BaseUrl { get; set; } = "https://fapi.binance.com";
}