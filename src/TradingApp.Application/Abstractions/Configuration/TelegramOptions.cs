namespace TradingApp.Application.Abstractions.Configuration;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
}
