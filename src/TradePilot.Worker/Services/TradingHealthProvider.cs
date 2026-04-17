namespace TradePilot.Worker.Services;

public sealed class TradingHealthProvider : ITradingHealthProvider
{
    private volatile bool _isConnected;
    private DateTimeOffset? _lastTradeReceived;
    private DateTimeOffset? _lastCandleClosed;
    private DateTimeOffset _serviceStartedUtc;
    private readonly object _lock = new();

    public bool IsWebSocketConnected => _isConnected;

    public DateTimeOffset? LastTradeReceived
    {
        get { lock (_lock) return _lastTradeReceived; }
    }

    public DateTimeOffset? LastCandleClosed
    {
        get { lock (_lock) return _lastCandleClosed; }
    }

    public DateTimeOffset ServiceStartedUtc => _serviceStartedUtc;

    public void RecordTradeReceived()
    {
        lock (_lock)
        {
            _lastTradeReceived = DateTimeOffset.UtcNow;
        }
    }

    public void RecordCandleClosed(string timeframe)
    {
        lock (_lock)
        {
            _lastCandleClosed = DateTimeOffset.UtcNow;
        }
    }

    public void RecordConnectionState(bool connected)
    {
        _isConnected = connected;
    }

    public void RecordServiceStarted()
    {
        _serviceStartedUtc = DateTimeOffset.UtcNow;
    }

    public TradingHealthSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            return new TradingHealthSnapshot(
                IsWebSocketConnected: _isConnected,
                LastTradeReceived: _lastTradeReceived,
                LastCandleClosed: _lastCandleClosed,
                ServiceStartedUtc: _serviceStartedUtc,
                Uptime: now - _serviceStartedUtc,
                TimeSinceLastTrade: _lastTradeReceived.HasValue ? now - _lastTradeReceived.Value : null,
                TimeSinceLastCandle: _lastCandleClosed.HasValue ? now - _lastCandleClosed.Value : null);
        }
    }
}
