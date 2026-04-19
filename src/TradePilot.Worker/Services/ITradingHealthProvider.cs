namespace TradePilot.Worker.Services;

public interface ITradingHealthProvider
{
    bool IsWebSocketConnected { get; }
    bool IsTradingSessionActive { get; }
    DateTimeOffset? LastTradeReceived { get; }
    DateTimeOffset? LastCandleClosed { get; }
    DateTimeOffset ServiceStartedUtc { get; }
    DateTimeOffset? TradingSessionStartedUtc { get; }

    void RecordTradeReceived();
    void RecordCandleClosed(string timeframe);
    void RecordConnectionState(bool connected);
    void RecordServiceStarted();
    void RecordTradingSessionStarted();
    void RecordTradingSessionStopped();

    TradingHealthSnapshot GetSnapshot();
}

public sealed record TradingHealthSnapshot(
    bool IsWebSocketConnected,
    bool IsTradingSessionActive,
    DateTimeOffset? LastTradeReceived,
    DateTimeOffset? LastCandleClosed,
    DateTimeOffset ServiceStartedUtc,
    DateTimeOffset? TradingSessionStartedUtc,
    TimeSpan Uptime,
    TimeSpan? TradingSessionUptime,
    TimeSpan? TimeSinceLastTrade,
    TimeSpan? TimeSinceLastCandle);
