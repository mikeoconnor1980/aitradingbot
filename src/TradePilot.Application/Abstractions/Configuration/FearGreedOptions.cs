using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class FearGreedOptions
{
    public const string SectionName = "FearGreed";

    [Required]
    public string BaseUrl { get; set; } = "https://api.alternative.me/";

    public bool Enabled { get; set; } = true;

    [Range(30, 1440)]
    public int SyncIntervalMinutes { get; set; } = 360;

    [Range(1, 168)]
    public int StalenessThresholdHours { get; set; } = 48;
}
