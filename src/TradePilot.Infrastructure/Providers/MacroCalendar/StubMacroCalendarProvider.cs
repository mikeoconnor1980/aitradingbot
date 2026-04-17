using System.Text.Json;
using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.MacroCalendar.Services;

namespace TradePilot.Infrastructure.Providers.MacroCalendar;

/// <summary>
/// Generates deterministic fake macro events for development.
/// Swap this out for FinnhubMacroCalendarProvider (or any real provider) when ready.
/// </summary>
public sealed class StubMacroCalendarProvider : IMacroCalendarProvider
{
    public string ProviderName => "Stub";

    private static readonly (string Title, string Country, string Currency, string Category, string Importance)[] Templates =
    [
        ("US CPI (YoY)", "United States", "USD", "inflation", "high"),
        ("FOMC Interest Rate Decision", "United States", "USD", "rates", "critical"),
        ("Non-Farm Payrolls", "United States", "USD", "labour", "high"),
        ("US GDP (QoQ)", "United States", "USD", "growth", "high"),
        ("US PCE Price Index", "United States", "USD", "inflation", "high"),
        ("Fed Chair Powell Speech", "United States", "USD", "rates", "high"),
        ("ECB Interest Rate Decision", "Euro Area", "EUR", "rates", "high"),
        ("BoE Interest Rate Decision", "United Kingdom", "GBP", "rates", "medium"),
        ("Eurozone CPI (YoY)", "Euro Area", "EUR", "inflation", "medium"),
        ("US Initial Jobless Claims", "United States", "USD", "labour", "medium"),
        ("US ISM Manufacturing PMI", "United States", "USD", "general", "medium"),
        ("US Retail Sales (MoM)", "United States", "USD", "general", "medium"),
        ("Japan GDP (QoQ)", "Japan", "JPY", "growth", "low"),
        ("UK CPI (YoY)", "United Kingdom", "GBP", "inflation", "medium"),
        ("Australia Employment Change", "Australia", "AUD", "labour", "low"),
    ];

    public Task<IReadOnlyCollection<ExternalMacroEventDto>> GetEventsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var events = new List<ExternalMacroEventDto>();
        var dayCount = (int)(toUtc.Date - fromUtc.Date).TotalDays + 1;

        for (var dayOffset = 0; dayOffset < dayCount; dayOffset++)
        {
            var date = fromUtc.Date.AddDays(dayOffset);

            // Skip weekends
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            // Pick 2-4 events per day using day-of-year as seed for determinism
            var seed = date.DayOfYear * 31 + date.Year;
            var rng = new Random(seed);
            var eventsPerDay = rng.Next(2, 5);

            for (var i = 0; i < eventsPerDay; i++)
            {
                var template = Templates[rng.Next(Templates.Length)];
                var hour = 8 + rng.Next(0, 7); // events between 08:00 and 14:00 UTC
                var minute = rng.Next(0, 4) * 15; // 00, 15, 30, 45
                var scheduledAt = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero);

                if (scheduledAt < fromUtc || scheduledAt > toUtc)
                    continue;

                var scheduledMs = scheduledAt.ToUnixTimeMilliseconds();
                var isPast = scheduledAt < DateTimeOffset.UtcNow;

                events.Add(new ExternalMacroEventDto
                {
                    ProviderEventId = $"stub|{template.Title}|{scheduledMs}",
                    Title = template.Title,
                    Country = template.Country,
                    Currency = template.Currency,
                    Category = template.Category,
                    ScheduledAtUtcMs = scheduledMs,
                    ReleasedAtUtcMs = isPast ? scheduledMs + 60_000 : null,
                    ImportanceRaw = template.Importance,
                    StatusRaw = isPast ? "released" : "scheduled",
                    Actual = isPast ? GenerateFakeValue(rng) : null,
                    Forecast = GenerateFakeValue(rng),
                    Previous = GenerateFakeValue(rng),
                    RawPayloadJson = JsonSerializer.Serialize(new { source = "stub", template.Title, scheduledMs }),
                });
            }
        }

        return Task.FromResult<IReadOnlyCollection<ExternalMacroEventDto>>(events);
    }

    private static string GenerateFakeValue(Random rng)
    {
        var value = Math.Round(rng.NextDouble() * 5 - 1, 1);
        return $"{value}%";
    }
}
