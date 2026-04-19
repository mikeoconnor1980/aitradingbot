namespace TradePilot.Worker.Services;

public sealed class TradingHealthProvider : ITradingHealthProvider
{
    private volatile bool _isConnected;
    private volatile bool _isTradingSessionActive;
    private DateTimeOffset? _lastTradeReceived;
    private DateTimeOffset? _lastCandleClosed;
    private DateTimeOffset _serviceStartedUtc;
    private DateTimeOffset? _tradingSessionStartedUtc;
    private readonly object _lock = new();

    public bool IsWebSocketConnected => _isConnected;
    public bool IsTradingSessionActive => _isTradingSessionActive;

    public DateTimeOffset? LastTradeReceived
    {
        get { lock (_lock) return _lastTradeReceived; }
    }

    public DateTimeOffset? LastCandleClosed
    {
        get { lock (_lock) return _lastCandleClosed; }
    }

    public DateTimeOffset ServiceStartedUtc => _serviceStartedUtc;

    public DateTimeOffset? TradingSessionStartedUtc
    {
        get { lock (_lock) return _tradingSessionStartedUtc; }
    }

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

    public void RecordTradingSessionStarted()
    {
        lock (_lock)
        {
            _isTradingSessionActive = true;
            _isConnected = false;
            _lastTradeReceived = null;
            _lastCandleClosed = null;
            _tradingSessionStartedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordTradingSessionStopped()
    {
        lock (_lock)
        {
            _isTradingSessionActive = false;
            _isConnected = false;
            _lastTradeReceived = null;
            _lastCandleClosed = null;
            _tradingSessionStartedUtc = null;
        }
    }

    public TradingHealthSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            return new TradingHealthSnapshot(
                IsWebSocketConnected: _isConnected,
                IsTradingSessionActive: _isTradingSessionActive,
                LastTradeReceived: _lastTradeReceived,
                LastCandleClosed: _lastCandleClosed,
                ServiceStartedUtc: _serviceStartedUtc,
                TradingSessionStartedUtc: _tradingSessionStartedUtc,
                Uptime: now - _serviceStartedUtc,
                TradingSessionUptime: _tradingSessionStartedUtc.HasValue ? now - _tradingSessionStartedUtc.Value : null,
                TimeSinceLastTrade: _lastTradeReceived.HasValue ? now - _lastTradeReceived.Value : null,
                TimeSinceLastCandle: _lastCandleClosed.HasValue ? now - _lastCandleClosed.Value : null);
        }
    }
}
