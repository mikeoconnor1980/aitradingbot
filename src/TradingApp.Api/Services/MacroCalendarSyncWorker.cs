using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingApp.Application.MacroCalendar.Configuration;
using TradingApp.Application.MacroCalendar.Services;
using TradingApp.Domain.Enums;
using TradingApp.Persistence;

namespace TradingApp.Api.Services;

public sealed class MacroCalendarSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MacroCalendarOptions _options;
    private readonly ILogger<MacroCalendarSyncWorker> _logger;

    private DateTimeOffset _lastFullSync = DateTimeOffset.MinValue;

    public MacroCalendarSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MacroCalendarOptions> options,
        ILogger<MacroCalendarSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Macro calendar sync worker is disabled");
            return;
        }

        _logger.LogInformation("Macro calendar sync worker starting (provider={Provider})", _options.Provider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<IMacroCalendarIngestionService>();
                var db = scope.ServiceProvider.GetRequiredService<TradingAppDbContext>();

                var now = DateTimeOffset.UtcNow;
                var timeSinceFullSync = now - _lastFullSync;

                if (timeSinceFullSync >= TimeSpan.FromMinutes(_options.FullSyncIntervalMinutes))
                {
                    var from = now.AddDays(-_options.LookBackDays);
                    var to = now.AddDays(_options.LookAheadDays);
                    await ingestion.SyncAsync(from, to, stoppingToken);
                    _lastFullSync = now;

                    await Task.Delay(
                        TimeSpan.FromMinutes(_options.IncrementalSyncIntervalMinutes),
                        stoppingToken);
                    continue;
                }

                var nowMs = now.ToUnixTimeMilliseconds();
                var nearWindowMs = nowMs + (_options.NearEventWindowMinutes * 60_000L);

                var nearEventExists = await db.MacroEvents.AnyAsync(x =>
                    x.Importance >= MacroEventImportance.High &&
                    x.ScheduledAtUtc >= nowMs &&
                    x.ScheduledAtUtc <= nearWindowMs,
                    stoppingToken);

                if (nearEventExists)
                {
                    await ingestion.SyncAsync(now.AddHours(-1), now.AddHours(6), stoppingToken);
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.NearEventSyncIntervalSeconds),
                        stoppingToken);
                }
                else
                {
                    await ingestion.SyncAsync(now.AddHours(-6), now.AddDays(1), stoppingToken);
                    await Task.Delay(
                        TimeSpan.FromMinutes(_options.IncrementalSyncIntervalMinutes),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MacroCalendar.SyncFailed — retrying in 60s");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
