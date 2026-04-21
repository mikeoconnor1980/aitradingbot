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
    private int _healthyCheckCount;

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
        if (!snapshot.IsTradingSessionActive)
        {
            _healthyCheckCount = 0;
            _logger.LogInformation(
                "HEALTH: Idle — no active trading session. Uptime={Uptime:hh\\:mm\\:ss}",
                snapshot.Uptime);
            return;
        }

        var sessionUptime = snapshot.TradingSessionUptime ?? TimeSpan.Zero;
        var hasHealthIssue = false;

        // WebSocket disconnected
        if (!snapshot.IsWebSocketConnected)
        {
            hasHealthIssue = true;
            _logger.LogWarning(
                "HEALTH: Market WebSocket is DISCONNECTED. SessionUptime={SessionUptime:hh\\:mm\\:ss}",
                sessionUptime);
        }

        // No trades received ever (after startup grace period)
        if (snapshot.LastTradeReceived is null && sessionUptime > TradeStaleThreshold)
        {
            hasHealthIssue = true;
            _logger.LogWarning(
                "HEALTH: No trades received since session start ({SessionUptime:hh\\:mm\\:ss} ago).",
                sessionUptime);
        }
        // Trades were received but have gone stale
        else if (snapshot.TimeSinceLastTrade > TradeStaleThreshold)
        {
            hasHealthIssue = true;
            _logger.LogWarning(
                "HEALTH: Trade stream appears stale. Last trade {TimeSince:hh\\:mm\\:ss} ago.",
                snapshot.TimeSinceLastTrade.Value);
        }

        // Candles have gone stale (only warn if we've received at least one candle)
        if (snapshot.LastCandleClosed is not null && snapshot.TimeSinceLastCandle > CandleStaleThreshold)
        {
            hasHealthIssue = true;
            _logger.LogWarning(
                "HEALTH: No candle closed in {TimeSince:hh\\:mm\\:ss}. Expected within {Threshold}.",
                snapshot.TimeSinceLastCandle.Value, CandleStaleThreshold);
        }

        if (hasHealthIssue)
        {
            _healthyCheckCount = 0;
            return;
        }

        _healthyCheckCount++;
        if (_healthyCheckCount % 10 == 0)
        {
            _logger.LogInformation(
                "HEALTH: OK — Connected={Connected}, LastTrade={LastTrade}, LastCandle={LastCandle}, Uptime={Uptime:hh\\:mm\\:ss}",
                snapshot.IsWebSocketConnected,
                snapshot.TimeSinceLastTrade?.ToString("hh\\:mm\\:ss") ?? "none",
                snapshot.TimeSinceLastCandle?.ToString("hh\\:mm\\:ss") ?? "none",
                sessionUptime);
        }
    }
}
