using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;

namespace TradePilot.Infrastructure.Services;

/// <summary>
/// Sends Telegram notifications via the Bot API.
/// All errors are logged and swallowed — notification failure must never disrupt trading.
/// </summary>
public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(HttpClient httpClient, ILogger<TelegramNotifier> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task NotifyFillAsync(long chatId, FillEventDto fill, CancellationToken cancellationToken = default)
    {
        await NotifyFillBatchAsync(chatId, [fill], cancellationToken);
    }

    public async Task NotifyFillBatchAsync(long chatId, IReadOnlyList<FillEventDto> fills, CancellationToken cancellationToken = default)
    {
        if (fills.Count == 0) return;

        var groups = fills
            .GroupBy(f => new { f.Asset, f.Side, f.Direction })
            .Select(g =>
            {
                var totalSize = g.Sum(f => f.Size);
                var totalPnl = g.Sum(f => f.ClosedPnl);
                var vwap = g.Sum(f => f.Size * f.Price) / totalSize;
                var isClose = totalPnl != 0;
                var emoji = g.Key.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "\U0001f7e2" : "\U0001f534";
                var direction = isClose ? "CLOSED" : g.Key.Side.ToUpperInvariant();
                var partialNote = g.Count() > 1 ? $" ({g.Count()} fills)" : "";

                return isClose
                    ? $"{emoji} *{direction}* \u2014 {Escape(g.Key.Asset)}{partialNote}\n" +
                      $"Size: {totalSize} @ ${vwap:N2}\n" +
                      $"PnL: {(totalPnl >= 0 ? "+" : "")}{totalPnl:N2} USD"
                    : $"{emoji} *{direction}* \u2014 {Escape(g.Key.Asset)}{partialNote}\n" +
                      $"Size: {totalSize} @ ${vwap:N2}";
            });

        var text = string.Join("\n\n", groups);
        await SendMessageAsync(chatId, text, cancellationToken);
    }

    public async Task NotifyRiskEventAsync(long chatId, string eventType, string message, CancellationToken cancellationToken = default)
    {
        var text = $"⚠️ *Risk Alert* — {Escape(eventType)}\n{Escape(message)}";
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

        var text = $"{emoji} *Strategy {Escape(eventType)}* — {Escape(strategyName)}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            text += $"\n{Escape(detail)}";
        }

        await SendMessageAsync(chatId, text, cancellationToken);
    }

    private async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                chat_id = chatId,
                text,
                parse_mode = "Markdown",
                disable_web_page_preview = true,
            };

            var response = await _httpClient.PostAsJsonAsync("sendMessage", payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Telegram sendMessage failed: {StatusCode} — {Body}",
                    response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification to chat {ChatId}", chatId);
        }
    }

    private static string Escape(string text)
    {
        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");
    }
}
