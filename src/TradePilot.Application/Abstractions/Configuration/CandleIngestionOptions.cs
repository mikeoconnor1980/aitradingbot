using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class CandleIngestionOptions
{
    public const string SectionName = "CandleIngestion";

    [Range(0, 10000)]
    public int BatchDelayMs { get; set; } = 200;

    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(60000, 86400000)]
    public int MaxIngestionTimeoutMs { get; set; } = 900000;

    [Required]
    public DateTime DefaultStartDate { get; set; } = new(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc);
}
