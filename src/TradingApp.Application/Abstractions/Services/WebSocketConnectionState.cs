namespace TradingApp.Application.Abstractions.Services;

public enum WebSocketConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}
