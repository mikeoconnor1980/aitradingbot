using Microsoft.EntityFrameworkCore;
using TradePilot.Application.MacroCalendar.Models;
using TradePilot.Application.MacroCalendar.Services;
using TradePilot.Domain.Enums;
using TradePilot.Persistence;

namespace TradePilot.Persistence.Services;

public sealed class MacroCalendarQueryService : IMacroCalendarQueryService
{
    private readonly TradePilotDbContext _dbContext;

    public MacroCalendarQueryService(TradePilotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MacroEventListItemDto>> GetUpcomingEventsAsync(
        long fromUtcMs,
        long toUtcMs,
        string? currency,
        MacroEventImportance? minimumImportance,
        CancellationToken cancellationToken)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var query = _dbContext.MacroEvents
            .Where(e => e.ScheduledAtUtc >= fromUtcMs && e.ScheduledAtUtc <= toUtcMs);

        if (!string.IsNullOrWhiteSpace(currency))
            query = query.Where(e => e.Currency == currency);

        if (minimumImportance.HasValue)
            query = query.Where(e => e.Importance >= minimumImportance.Value);

        var events = await query
            .OrderBy(e => e.ScheduledAtUtc)
            .Select(e => new MacroEventListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                Country = e.Country,
                Currency = e.Currency,
                Category = e.Category,
                ScheduledAtUtc = e.ScheduledAtUtc,
                Importance = e.Importance,
                Status = e.Status,
                Forecast = e.Forecast,
                Previous = e.Previous,
                Actual = e.Actual,
                BlockStartUtc = e.BlockStartUtc,
                BlockEndUtc = e.BlockEndUtc,
                IsBlockingNow = e.BlockStartUtc <= nowMs && e.BlockEndUtc >= nowMs,
            })
            .ToListAsync(cancellationToken);

        return events;
    }

    public async Task<IReadOnlyCollection<MacroEventListItemDto>> GetActiveBlockWindowsAsync(
        long nowUtcMs,
        CancellationToken cancellationToken)
    {
        var events = await _dbContext.MacroEvents
            .Where(e =>
                e.BlockStartUtc <= nowUtcMs &&
                e.BlockEndUtc >= nowUtcMs &&
                e.Importance >= MacroEventImportance.High)
            .OrderBy(e => e.ScheduledAtUtc)
            .Select(e => new MacroEventListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                Country = e.Country,
                Currency = e.Currency,
                Category = e.Category,
                ScheduledAtUtc = e.ScheduledAtUtc,
                Importance = e.Importance,
                Status = e.Status,
                Forecast = e.Forecast,
                Previous = e.Previous,
                Actual = e.Actual,
                BlockStartUtc = e.BlockStartUtc,
                BlockEndUtc = e.BlockEndUtc,
                IsBlockingNow = true,
            })
            .ToListAsync(cancellationToken);

        return events;
    }
}
