using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Persistence;

namespace TradePilot.Api.Services;

/// <summary>
/// Long-polls the Telegram Bot API for incoming /link commands.
/// Matches link codes to users and sets their TelegramChatId.
/// Only starts when a Telegram bot token is configured.
/// </summary>
public sealed class TelegramBotPollingService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotPollingService> _logger;
    private readonly string _botToken;
    private readonly HttpClient _httpClient;

    private long _lastUpdateId;

    public TelegramBotPollingService(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> telegramOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramBotPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _botToken = telegramOptions.Value.BotToken;
        _httpClient = httpClientFactory.CreateClient("TelegramBot");
        _httpClient.BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/");
        _httpClient.Timeout = TimeSpan.FromSeconds(35);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
        {
            _logger.LogInformation("Telegram bot token not configured. Polling service will not start.");
            return;
        }

        await EnsurePollingModeAsync(stoppingToken);

        _logger.LogInformation("TelegramBotPollingService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollUpdatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex) when (IsConflict(ex))
            {
                _logger.LogError(
                    ex,
                    "Telegram polling conflict detected. Another webhook or polling consumer is using this bot token. Polling service is stopping until the app restarts.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram polling error. Retrying in 5s.");
                try { await Task.Delay(5000, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("TelegramBotPollingService stopped.");
    }

    internal async Task EnsurePollingModeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                drop_pending_updates = false,
            };

            using var response = await _httpClient.PostAsJsonAsync("deleteWebhook", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram webhook cleared. Polling mode active.");
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Telegram deleteWebhook returned {StatusCode}. Polling will continue. Body={Body}",
                (int)response.StatusCode,
                body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear Telegram webhook before polling. Polling will continue.");
        }
    }

    internal static bool IsConflict(HttpRequestException exception)
    {
        return exception.StatusCode == HttpStatusCode.Conflict;
    }

    private async Task PollUpdatesAsync(CancellationToken cancellationToken)
    {
        var url = $"getUpdates?timeout=30&offset={_lastUpdateId + 1}&allowed_updates=[\"message\"]";
        var response = await _httpClient.GetFromJsonAsync<TelegramResponse>(url, JsonOptions, cancellationToken);

        if (response?.Result is null) return;

        foreach (var update in response.Result)
        {
            _lastUpdateId = Math.Max(_lastUpdateId, update.UpdateId);

            if (update.Message?.Text is null) continue;

            await HandleMessageAsync(update.Message, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim() ?? string.Empty;
        var chatId = message.Chat.Id;

        // Handle /start — welcome message
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await SendReplyAsync(chatId,
                "👋 Welcome to TradePilot\\!\n\n" +
                "To link your account, generate a code from the app settings and send:\n" +
                "`/link YOUR_CODE`",
                cancellationToken);
            return;
        }

        // Handle /link CODE
        if (text.StartsWith("/link ", StringComparison.OrdinalIgnoreCase))
        {
            var code = text[6..].Trim();
            await HandleLinkCodeAsync(chatId, code, cancellationToken);
            return;
        }
    }

    private async Task HandleLinkCodeAsync(long chatId, string code, CancellationToken cancellationToken)
    {
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            await SendReplyAsync(chatId, "❌ Invalid code format\\. Please enter a 6\\-digit code\\.", cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var linkCode = await db.TelegramLinkCodes
            .FirstOrDefaultAsync(c => c.Code == code && !c.IsUsed, cancellationToken);

        if (linkCode is null || linkCode.IsExpired(nowMs))
        {
            await SendReplyAsync(chatId, "❌ Invalid or expired code\\. Generate a new one from the app\\.", cancellationToken);
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == linkCode.UserId, cancellationToken);
        if (user is null)
        {
            await SendReplyAsync(chatId, "❌ User not found\\. Please try again\\.", cancellationToken);
            return;
        }

        linkCode.MarkUsed();
        user.LinkTelegram(chatId);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Telegram linked for user {UserId} → chat {ChatId}", user.Id, chatId);

        await SendReplyAsync(chatId,
            "✅ *Linked to TradePilot\\!*\n\nYou'll receive trade notifications here\\.",
            cancellationToken);
    }

    private async Task SendReplyAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                chat_id = chatId,
                text,
                parse_mode = "MarkdownV2",
            };

            await _httpClient.PostAsJsonAsync("sendMessage", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram reply to chat {ChatId}", chatId);
        }
    }

    // --- Telegram API response models ---

    private sealed class TelegramResponse
    {
        public bool Ok { get; set; }
        public List<TelegramUpdate>? Result { get; set; }
    }

    private sealed class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }
        public TelegramMessage? Message { get; set; }
    }

    private sealed class TelegramMessage
    {
        public string? Text { get; set; }
        public TelegramChat Chat { get; set; } = default!;
    }

    private sealed class TelegramChat
    {
        public long Id { get; set; }
    }
}
