using Microsoft.EntityFrameworkCore;
using TradePilot.Persistence;

namespace TradePilot.Api.Services;

public sealed class ExecutionLogCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExecutionLogCleanupService> _logger;

    public ExecutionLogCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExecutionLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();

                var cutoff = DateTimeOffset.UtcNow - Retention;
                var deleted = await db.ExecutionLogs
                    .Where(e => e.ReceivedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Purged {Count} execution log entries older than {Cutoff:u}", deleted, cutoff);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging execution logs");
            }
        }
    }
}
