using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Worker.Services;

/// <summary>
/// Telegram notifier that reads the bot token dynamically from <see cref="NotificationConfigHolder"/>.
/// This allows the Worker to receive the token via heartbeat instead of requiring local configuration.
/// </summary>
public sealed class DynamicTelegramNotifier : ITelegramNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NotificationConfigHolder _config;
    private readonly ILogger<DynamicTelegramNotifier> _logger;

    public DynamicTelegramNotifier(
        IHttpClientFactory httpClientFactory,
        NotificationConfigHolder config,
        ILogger<DynamicTelegramNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyFillAsync(long chatId, FillEventDto fill, CancellationToken cancellationToken = default)
    {
        await NotifyFillBatchAsync(chatId, [fill], cancellationToken);
    }

    public async Task NotifyFillBatchAsync(long chatId, IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default)
    {
        if (fills.Count == 0) return;

        // Group partial fills by asset+side+direction into consolidated lines
        var groups = fills
            .GroupBy(f => new { f.Asset, f.Side, f.Direction })
            .Select(g =>
            {
                var totalSize = g.Sum(f => f.Size);
                var totalPnl = g.Sum(f => f.ClosedPnl);
                var totalFee = g.Sum(f => f.Fee);
                var vwap = g.Sum(f => f.Size * f.Price) / totalSize;
                var isClose = totalPnl != 0;
                var emoji = g.Key.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "🟢" : "🔴";
                var direction = isClose ? "CLOSED" : g.Key.Side.ToUpperInvariant();
                var partialNote = g.Count() > 1 ? $" ({g.Count()} fills)" : "";

                return isClose
                                        ? $"{emoji} <b>{direction}</b> - {Escape(g.Key.Asset)}{partialNote}\n" +
                      $"Size: {totalSize} @ ${vwap:N2}\n" +
                      $"PnL: {(totalPnl >= 0 ? "+" : "")}{totalPnl:N2} USD | Fee: {totalFee:N4}"
                                        : $"{emoji} <b>{direction}</b> - {Escape(g.Key.Asset)}{partialNote}\n" +
                      $"Size: {totalSize} @ ${vwap:N2}";
            });

        var text = string.Join("\n\n", groups);
        await SendMessageAsync(chatId, text, cancellationToken);
    }

    public async Task NotifyRiskEventAsync(long chatId, string eventType, string message, CancellationToken cancellationToken = default)
    {
        var text = $"⚠️ <b>Risk Alert</b> - {Escape(eventType)}\n{Escape(message)}";
        await SendMessageAsync(chatId, text, cancellationToken);
    }

    public async Task NotifyStrategyEventAsync(long chatId, string eventType, string strategyName, string? detail = null, CancellationToken cancellationToken = default)
    {
        var emoji = eventType switch
        {
            "started" => "🚀",
            "stopped" => "⏹️",
            "grid_deployed" => "📊",
            _ => "ℹ️",
        };

        var text = $"{emoji} <b>Strategy {Escape(eventType)}</b> - {Escape(strategyName)}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            text += $"\n{Escape(detail)}";
        }

        await SendMessageAsync(chatId, text, cancellationToken);
    }

    private async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var botToken = _config.TelegramBotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogDebug("Telegram bot token not available from heartbeat — skipping send");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("TelegramBot");
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text,
                parse_mode = "HTML",
                disable_web_page_preview = true,
            };

            var response = await client.PostAsJsonAsync(url, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Telegram API returned {StatusCode}: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification to chat {ChatId}", chatId);
        }
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
