using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.MacroCalendar.Configuration;

public sealed class MacroCalendarOptions
{
    public const string SectionName = "MacroCalendar";

    [Required]
    public string Provider { get; set; } = "Stub";

    public bool Enabled { get; set; } = true;

    [Range(1, 30)]
    public int LookAheadDays { get; set; } = 7;

    [Range(0, 7)]
    public int LookBackDays { get; set; } = 1;

    [Range(1, 60)]
    public int NearEventWindowMinutes { get; set; } = 10;

    [Range(30, 1440)]
    public int FullSyncIntervalMinutes { get; set; } = 720;

    [Range(1, 1440)]
    public int IncrementalSyncIntervalMinutes { get; set; } = 720;

    [Range(10, 300)]
    public int NearEventSyncIntervalSeconds { get; set; } = 60;

    public MacroPolicyOptions DefaultPolicies { get; set; } = new();
}

public sealed class MacroPolicyOptions
{
    public MacroBlockPolicy High { get; set; } = new() { PreBlockMinutes = 30, PostBlockMinutes = 30 };
    public MacroBlockPolicy Medium { get; set; } = new() { PreBlockMinutes = 10, PostBlockMinutes = 10 };
    public MacroBlockPolicy Low { get; set; } = new();
}

public sealed class MacroBlockPolicy
{
    public int PreBlockMinutes { get; set; }
    public int PostBlockMinutes { get; set; }
}
