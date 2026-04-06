namespace TradingApp.Worker.Services;

public interface ITradingHealthProvider
{
    bool IsWebSocketConnected { get; }
    DateTimeOffset? LastTradeReceived { get; }
    DateTimeOffset? LastCandleClosed { get; }
    DateTimeOffset ServiceStartedUtc { get; }

    void RecordTradeReceived();
    void RecordCandleClosed(string timeframe);
    void RecordConnectionState(bool connected);
    void RecordServiceStarted();

    TradingHealthSnapshot GetSnapshot();
}

public sealed record TradingHealthSnapshot(
    bool IsWebSocketConnected,
    DateTimeOffset? LastTradeReceived,
    DateTimeOffset? LastCandleClosed,
    DateTimeOffset ServiceStartedUtc,
    TimeSpan Uptime,
    TimeSpan? TimeSinceLastTrade,
    TimeSpan? TimeSinceLastCandle);
