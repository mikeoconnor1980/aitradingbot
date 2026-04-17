namespace TradingApp.Worker.Services;

/// <summary>
/// Thread-safe holder for notification configuration received from the control plane heartbeat.
/// Written by AgentCheckInService, read by UserEventStreamService and TradingSession.
/// </summary>
public sealed class NotificationConfigHolder
{
    private readonly object _lock = new();
    private long? _telegramChatId;
    private string? _telegramBotToken;

    public long? TelegramChatId
    {
        get { lock (_lock) { return _telegramChatId; } }
        set { lock (_lock) { _telegramChatId = value; } }
    }

    public string? TelegramBotToken
    {
        get { lock (_lock) { return _telegramBotToken; } }
        set { lock (_lock) { _telegramBotToken = value; } }
    }
}
