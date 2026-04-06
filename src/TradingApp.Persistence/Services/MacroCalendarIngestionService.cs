using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingApp.Application.MacroCalendar.Models;
using TradingApp.Application.MacroCalendar.Services;
using TradingApp.Domain.Entities;
using TradingApp.Persistence;

namespace TradingApp.Persistence.Services;

public sealed class MacroCalendarIngestionService : IMacroCalendarIngestionService
{
    private readonly IMacroCalendarProvider _provider;
    private readonly TradingAppDbContext _dbContext;
    private readonly IMacroBlockWindowCalculator _windowCalculator;
    private readonly ILogger<MacroCalendarIngestionService> _logger;

    public MacroCalendarIngestionService(
        IMacroCalendarProvider provider,
        TradingAppDbContext dbContext,
        IMacroBlockWindowCalculator windowCalculator,
        ILogger<MacroCalendarIngestionService> logger)
    {
        _provider = provider;
        _dbContext = dbContext;
        _windowCalculator = windowCalculator;
        _logger = logger;
    }

    public async Task<MacroSyncResult> SyncAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var syncRun = MacroSyncRun.Start(_provider.ProviderName);
        _dbContext.MacroSyncRuns.Add(syncRun);

        try
        {
            _logger.LogInformation(
                "MacroCalendar.SyncStarted: provider={Provider} from={From} to={To}",
                _provider.ProviderName, fromUtc, toUtc);

            var externalEvents = await _provider.GetEventsAsync(fromUtc, toUtc, cancellationToken);

            var result = new MacroSyncResult { Fetched = externalEvents.Count };

            foreach (var external in externalEvents)
            {
                var importance = MacroImportanceMapper.Map(external.ImportanceRaw);
                var status = MacroStatusMapper.Map(external.StatusRaw);
                var (pre, post) = _windowCalculator.GetWindow(importance, external.Category);

                var existing = await _dbContext.MacroEvents
                    .FirstOrDefaultAsync(x =>
                        x.Provider == _provider.ProviderName &&
                        x.ProviderEventId == external.ProviderEventId,
                        cancellationToken);

                if (existing is null)
                {
                    var entity = MacroEvent.Create(
                        _provider.ProviderName,
                        external.ProviderEventId,
                        external.Title,
                        external.Country,
                        external.Currency,
                        external.Category,
                        external.ScheduledAtUtcMs,
                        importance,
                        pre,
                        post);

                    entity.SetInitialData(
                        external.Actual,
                        external.Forecast,
                        external.Previous,
                        external.SourceUrl,
                        external.RawPayloadJson);

                    _dbContext.MacroEvents.Add(entity);
                    result.Inserted++;
                }
                else
                {
                    existing.Update(
                        external.Title,
                        external.Country,
                        external.Currency,
                        external.Category,
                        external.ScheduledAtUtcMs,
                        importance,
                        status,
                        pre,
                        post,
                        external.Actual,
                        external.Forecast,
                        external.Previous,
                        external.Revised,
                        external.ReleasedAtUtcMs,
                        external.SourceUrl,
                        external.RawPayloadJson);

                    result.Updated++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            syncRun.Complete(result.Fetched, result.Inserted, result.Updated);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "MacroCalendar.SyncCompleted: fetched={Fetched} inserted={Inserted} updated={Updated}",
                result.Fetched, result.Inserted, result.Updated);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            syncRun.Fail(ex.Message);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "MacroCalendar.SyncFailed");
            throw;
        }
    }
}
