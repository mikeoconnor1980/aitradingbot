using TradePilot.Domain.Enums;

namespace TradePilot.Domain.Entities;

public sealed class MacroEvent
{
    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderEventId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public long ScheduledAtUtc { get; private set; }
    public long? ReleasedAtUtc { get; private set; }
    public MacroEventImportance Importance { get; private set; }
    public MacroEventStatus Status { get; private set; }
    public string? Actual { get; private set; }
    public string? Forecast { get; private set; }
    public string? Previous { get; private set; }
    public string? Revised { get; private set; }
    public int DefaultPreBlockMinutes { get; private set; }
    public int DefaultPostBlockMinutes { get; private set; }
    public long BlockStartUtc { get; private set; }
    public long BlockEndUtc { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? RawPayloadJson { get; private set; }
    public long LastSeenUtc { get; private set; }
    public long CreatedAtUtc { get; private set; }
    public long UpdatedAtUtc { get; private set; }

    private MacroEvent()
    {
    }

    public static MacroEvent Create(
        string provider,
        string providerEventId,
        string title,
        string country,
        string currency,
        string category,
        long scheduledAtUtc,
        MacroEventImportance importance,
        int preBlockMinutes,
        int postBlockMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new MacroEvent
        {
            Id = Guid.NewGuid(),
            Provider = provider,
            ProviderEventId = providerEventId,
            Title = title,
            Country = country,
            Currency = currency,
            Category = category,
            ScheduledAtUtc = scheduledAtUtc,
            Importance = importance,
            Status = MacroEventStatus.Scheduled,
            DefaultPreBlockMinutes = preBlockMinutes,
            DefaultPostBlockMinutes = postBlockMinutes,
            BlockStartUtc = scheduledAtUtc - (preBlockMinutes * 60_000L),
            BlockEndUtc = scheduledAtUtc + (postBlockMinutes * 60_000L),
            LastSeenUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string title,
        string country,
        string currency,
        string category,
        long scheduledAtUtc,
        MacroEventImportance importance,
        MacroEventStatus status,
        int preBlockMinutes,
        int postBlockMinutes,
        string? actual,
        string? forecast,
        string? previous,
        string? revised,
        long? releasedAtUtc,
        string? sourceUrl,
        string? rawPayloadJson)
    {
        Title = title;
        Country = country;
        Currency = currency;
        Category = category;
        ScheduledAtUtc = scheduledAtUtc;
        Importance = importance;
        Status = status;
        DefaultPreBlockMinutes = preBlockMinutes;
        DefaultPostBlockMinutes = postBlockMinutes;
        BlockStartUtc = scheduledAtUtc - (preBlockMinutes * 60_000L);
        BlockEndUtc = scheduledAtUtc + (postBlockMinutes * 60_000L);
        Actual = actual;
        Forecast = forecast;
        Previous = previous;
        Revised = revised;
        ReleasedAtUtc = releasedAtUtc;
        SourceUrl = sourceUrl;
        RawPayloadJson = rawPayloadJson;
        LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UpdatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void SetInitialData(
        string? actual,
        string? forecast,
        string? previous,
        string? sourceUrl,
        string? rawPayloadJson)
    {
        Actual = actual;
        Forecast = forecast;
        Previous = previous;
        SourceUrl = sourceUrl;
        RawPayloadJson = rawPayloadJson;
    }

    public bool IsBlockingAt(long timestampUtcMs)
    {
        return BlockStartUtc <= timestampUtcMs && BlockEndUtc >= timestampUtcMs;
    }
}
