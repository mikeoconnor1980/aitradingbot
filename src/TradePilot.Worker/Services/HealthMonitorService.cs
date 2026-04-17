using Microsoft.Extensions.Logging;

namespace TradePilot.Worker.Services;

/// <summary>
/// Background watchdog that periodically checks trading health and logs warnings
/// when the system appears stale (no trades, no candles, WebSocket disconnected).
/// </summary>
public sealed class HealthMonitorService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TradeStaleThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CandleStaleThreshold = TimeSpan.FromMinutes(20);

    private readonly ITradingHealthProvider _healthProvider;
    private readonly ILogger<HealthMonitorService> _logger;

    public HealthMonitorService(
        ITradingHealthProvider healthProvider,
        ILogger<HealthMonitorService> logger)
    {
        _healthProvider = healthProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthProvider.RecordServiceStarted();
        _logger.LogInformation("HealthMonitorService started. Checking every {Interval}.", CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var snapshot = _healthProvider.GetSnapshot();
            EvaluateHealth(snapshot);
        }
    }

    internal void EvaluateHealth(TradingHealthSnapshot snapshot)
    {
        // WebSocket disconnected
        if (!snapshot.IsWebSocketConnected)
        {
            _logger.LogWarning(
                "HEALTH: WebSocket is DISCONNECTED. Uptime={Uptime:hh\\:mm\\:ss}",
                snapshot.Uptime);
        }

        // No trades received ever (after startup grace period)
        if (snapshot.LastTradeReceived is null && snapshot.Uptime > TradeStaleThreshold)
        {
            _logger.LogWarning(
                "HEALTH: No trades received since startup ({Uptime:hh\\:mm\\:ss} ago).",
                snapshot.Uptime);
        }
        // Trades were received but have gone stale
        else if (snapshot.TimeSinceLastTrade > TradeStaleThreshold)
        {
            _logger.LogWarning(
                "HEALTH: Trade stream appears stale. Last trade {TimeSince:hh\\:mm\\:ss} ago.",
                snapshot.TimeSinceLastTrade.Value);
        }

        // Candles have gone stale (only warn if we've received at least one candle)
        if (snapshot.LastCandleClosed is not null && snapshot.TimeSinceLastCandle > CandleStaleThreshold)
        {
            _logger.LogWarning(
                "HEALTH: No candle closed in {TimeSince:hh\\:mm\\:ss}. Expected within {Threshold}.",
                snapshot.TimeSinceLastCandle.Value, CandleStaleThreshold);
        }

        // Periodic healthy status (every 10 checks ~= every 10 minutes)
        if (snapshot.IsWebSocketConnected
            && (snapshot.TimeSinceLastTrade is null || snapshot.TimeSinceLastTrade <= TradeStaleThreshold))
        {
            _logger.LogInformation(
                "HEALTH: OK — Connected={Connected}, LastTrade={LastTrade}, LastCandle={LastCandle}, Uptime={Uptime:hh\\:mm\\:ss}",
                snapshot.IsWebSocketConnected,
                snapshot.TimeSinceLastTrade?.ToString("hh\\:mm\\:ss") ?? "none",
                snapshot.TimeSinceLastCandle?.ToString("hh\\:mm\\:ss") ?? "none",
                snapshot.Uptime);
        }
    }
}
