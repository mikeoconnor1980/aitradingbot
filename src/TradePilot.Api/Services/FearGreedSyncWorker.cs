using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Api.Services;

public sealed class FearGreedSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FearGreedOptions _options;
    private readonly ILogger<FearGreedSyncWorker> _logger;

    public FearGreedSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FearGreedOptions> options,
        ILogger<FearGreedSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Fear & Greed sync worker is disabled.");
            return;
        }

        _logger.LogInformation("Fear & Greed sync worker starting (interval={IntervalMinutes}m).", _options.SyncIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IFearGreedClient>();
                var repository = scope.ServiceProvider.GetRequiredService<IFearGreedReadingRepository>();

                var readings = await client.FetchAsync(limit: 1, stoppingToken);

                if (readings.Count > 0)
                {
                    await repository.BulkUpsertAsync(readings, stoppingToken);
                    _logger.LogDebug("Fear & Greed sync completed, upserted {Count} reading(s).", readings.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Fear & Greed sync iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.SyncIntervalMinutes), stoppingToken);
        }
    }
}
