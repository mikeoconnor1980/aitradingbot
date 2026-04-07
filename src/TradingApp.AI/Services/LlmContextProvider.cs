using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.AI.Prompts;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MacroCalendar.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.AI.Services;

/// <summary>
/// Calls an LLM to produce qualitative market context and caches the result
/// for the configured duration. Falls back gracefully on LLM failure.
/// </summary>
public sealed class LlmContextProvider : ILlmContextProvider
{
    private readonly ILlmContextClient _llmClient;
    private readonly ILogger<LlmContextProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cacheDuration;
    private readonly object _cacheLock = new();

    private LlmContext? _cachedContext;
    private string? _cachedSymbol;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public LlmContextProvider(
        ILlmContextClient llmClient,
        IOptions<LlmContextOptions> options,
        ILogger<LlmContextProvider> logger,
        TimeProvider? timeProvider = null)
    {
        _llmClient = llmClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cacheDuration = TimeSpan.FromSeconds(options.Value.CacheDurationSeconds);
    }

    public async Task<LlmContext?> GetContextAsync(
        string symbol,
        IndicatorSnapshot indicators,
        IReadOnlyCollection<MacroEventListItemDto>? upcomingEvents = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(indicators);

        lock (_cacheLock)
        {
            if (_cachedContext is not null
                && string.Equals(_cachedSymbol, symbol, StringComparison.OrdinalIgnoreCase)
                && _timeProvider.GetUtcNow() < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached LLM context for {Symbol}, expires {Expiry}.", symbol, _cacheExpiry);
                return Task.FromResult<LlmContext?>(_cachedContext).Result;
            }
        }

        try
        {
            var userMessage = BuildUserMessage(symbol, indicators, upcomingEvents);

            var rawResponse = await _llmClient.CompleteAsync(
                MarketContextPrompt.SystemPrompt,
                userMessage,
                cancellationToken);

            var context = ParseResponse(rawResponse, symbol);

            lock (_cacheLock)
            {
                _cachedContext = context;
                _cachedSymbol = symbol;
                _cacheExpiry = _timeProvider.GetUtcNow().Add(_cacheDuration);
            }

            _logger.LogInformation(
                "LLM context updated for {Symbol}: regime={Regime}, sentiment={Sentiment}, confidence={Confidence:F2}",
                symbol,
                context.DerivedRegime,
                context.MarketSentiment,
                context.Confidence);

            return context;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "LLM context request failed for {Symbol}. Returning cached or null.", symbol);

            lock (_cacheLock)
            {
                return _cachedContext;
            }
        }
    }

    public static string BuildUserMessage(
        string symbol,
        IndicatorSnapshot indicators,
        IReadOnlyCollection<MacroEventListItemDto>? upcomingEvents = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Analyse the current market context for {symbol}.");
        builder.AppendLine();
        builder.AppendLine("Current indicator state:");
        builder.AppendLine($"- EMA(20): {indicators.EmaFast:F2}");
        builder.AppendLine($"- EMA(50): {indicators.EmaSlow:F2}");
        builder.AppendLine($"- EMA(200): {indicators.EmaTrend:F2}");
        builder.AppendLine($"- RSI(14): {indicators.Rsi:F2}");
        builder.AppendLine($"- ATR(14): {indicators.Atr:F4}");
        builder.AppendLine();

        var emaAlignment = (indicators.EmaFast, indicators.EmaSlow, indicators.EmaTrend) switch
        {
            var (f, s, t) when f > s && s > t => "Bullish stack (20 > 50 > 200)",
            var (f, s, t) when f < s && s < t => "Bearish stack (20 < 50 < 200)",
            _ => "Mixed / transitioning"
        };

        builder.AppendLine($"EMA alignment: {emaAlignment}");
        builder.AppendLine($"RSI zone: {(indicators.Rsi > 70 ? "Overbought" : indicators.Rsi < 30 ? "Oversold" : "Neutral")}");
        builder.AppendLine();

        AppendMacroEvents(builder, upcomingEvents);

        return builder.ToString();
    }

    private static void AppendMacroEvents(
        StringBuilder builder,
        IReadOnlyCollection<MacroEventListItemDto>? events)
    {
        if (events is null || events.Count == 0)
        {
            builder.AppendLine("Macro calendar: No upcoming macro events.");
            return;
        }

        var blocking = new List<MacroEventListItemDto>();
        var upcoming = new List<MacroEventListItemDto>();

        foreach (var evt in events)
        {
            if (evt.IsBlockingNow)
            {
                blocking.Add(evt);
            }
            else
            {
                upcoming.Add(evt);
            }
        }

        if (blocking.Count > 0)
        {
            builder.AppendLine("Active macro event block windows (trading should be restricted):");
            foreach (var evt in blocking)
            {
                builder.AppendLine($"- [{evt.Importance}] {evt.Title} ({evt.Country}/{evt.Currency}) — Category: {evt.Category}");
                AppendForecastAndPrevious(builder, evt);
            }

            builder.AppendLine();
        }

        if (upcoming.Count > 0)
        {
            builder.AppendLine("Upcoming macro events (next 24h):");
            foreach (var evt in upcoming)
            {
                var scheduledAt = DateTimeOffset.FromUnixTimeMilliseconds(evt.ScheduledAtUtc).UtcDateTime;
                builder.AppendLine($"- [{evt.Importance}] {evt.Title} ({evt.Country}/{evt.Currency}) — {scheduledAt:yyyy-MM-dd HH:mm} UTC — Category: {evt.Category}");
                AppendForecastAndPrevious(builder, evt);
            }

            builder.AppendLine();
        }
    }

    private static void AppendForecastAndPrevious(StringBuilder builder, MacroEventListItemDto evt)
    {
        if (evt.Forecast is not null || evt.Previous is not null)
        {
            var parts = new List<string>(2);
            if (evt.Forecast is not null) parts.Add($"Forecast: {evt.Forecast}");
            if (evt.Previous is not null) parts.Add($"Previous: {evt.Previous}");
            builder.AppendLine($"  {string.Join(", ", parts)}");
        }
    }

    public static LlmContext ParseResponse(string rawJson, string symbol)
    {
        // Strip code fences if present
        var json = rawJson.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
            {
                json = json[(firstNewline + 1)..];
            }

            if (json.EndsWith("```", StringComparison.Ordinal))
            {
                json = json[..^3];
            }

            json = json.Trim();
        }

        LlmContextResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LlmContextResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return CreateFallback($"Failed to parse LLM response for {symbol}.");
        }

        if (parsed is null)
        {
            return CreateFallback($"LLM returned null response for {symbol}.");
        }

        var regime = parsed.DerivedRegime?.Trim() switch
        {
            "Aggressive" => MarketRegime.Aggressive,
            "Normal" => MarketRegime.Normal,
            "Defensive" => MarketRegime.Defensive,
            "RiskOff" => MarketRegime.RiskOff,
            _ => MarketRegime.Normal
        };

        var confidence = Math.Clamp(parsed.Confidence, 0m, 1m);

        return new LlmContext
        {
            MarketSentiment = NormalizeSentiment(parsed.MarketSentiment),
            MacroRegime = NormalizeSentiment(parsed.MacroRegime),
            EventRisk = NormalizeEventRisk(parsed.EventRisk),
            Confidence = confidence,
            DerivedRegime = regime,
            Summary = parsed.Summary ?? string.Empty,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static LlmContext CreateFallback(string summary)
    {
        return new LlmContext
        {
            MarketSentiment = "Neutral",
            MacroRegime = "Neutral",
            EventRisk = "Low",
            Confidence = 0m,
            DerivedRegime = MarketRegime.Normal,
            Summary = summary,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static string NormalizeSentiment(string? value)
    {
        return value?.Trim() switch
        {
            "Bullish" => "Bullish",
            "Bearish" => "Bearish",
            _ => "Neutral"
        };
    }

    private static string NormalizeEventRisk(string? value)
    {
        return value?.Trim() switch
        {
            "High" => "High",
            "Medium" => "Medium",
            _ => "Low"
        };
    }

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    private sealed class LlmContextResponse
    {
        public string? MarketSentiment { get; set; }
        public string? MacroRegime { get; set; }
        public string? EventRisk { get; set; }
        public decimal Confidence { get; set; }
        public string? DerivedRegime { get; set; }
        public string? Summary { get; set; }
    }
}
