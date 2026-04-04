using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.AI.Prompts;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.AI.Services;

public sealed class StrategyReviewer : IStrategyReviewer
{
    private readonly IReviewLlmClient _llmClient;
    private readonly ILogger<StrategyReviewer> _logger;

    public StrategyReviewer(IReviewLlmClient llmClient, ILogger<StrategyReviewer> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<string> ReviewAsync(string strategyJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyJson);

        _logger.LogInformation(
            "Requesting AI strategy review for strategy JSON with length {Length}.",
            strategyJson.Length);

        try
        {
            var review = await _llmClient.CompleteAsync(
                StrategyReviewPrompt.SystemPrompt,
                BuildReviewRequest(strategyJson),
                cancellationToken);

            var normalizedReview = NormalizeReview(review, strategyJson);

            _logger.LogInformation("AI strategy review completed successfully.");

            return normalizedReview;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "LLM request failed during strategy review.");
            throw;
        }
    }

    private static string BuildReviewRequest(string strategyJson)
    {
        return $$"""
            Review the following trading strategy configuration JSON.

            Return only the final end-user markdown review.
            Do not repeat the JSON.
            Do not explain the prompt.
            Do not wrap the response in code fences.

            ```json
            {{strategyJson}}
            ```
            """;
    }

    private static string NormalizeReview(string review, string strategyJson)
    {
        var normalized = StripMarkdownFence(review).Trim();

        if (string.IsNullOrWhiteSpace(normalized) || LooksLikeRawJson(normalized))
        {
            return BuildFallbackReview(strategyJson);
        }

        return normalized;
    }

    private static string StripMarkdownFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0)
        {
            return trimmed;
        }

        return trimmed[(firstNewLine + 1)..^3].Trim();
    }

    private static bool LooksLikeRawJson(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static string BuildFallbackReview(string strategyJson)
    {
        try
        {
            using var document = JsonDocument.Parse(strategyJson);
            var root = document.RootElement;

            var strategyName = TryGetString(root, "strategyName") ?? "Unnamed strategy";
            var strategyMode = TryGetString(root, "strategyMode") ?? "unspecified";
            var market = TryGetString(root, "market") ?? "unknown market";
            var timeframe = TryGetString(root, "timeframe") ?? "unknown timeframe";
            var direction = TryGetString(root, "direction") ?? "unspecified direction";
            var entryConditions = TryGetArrayLength(root, "entryConditions");
            var leverage = TryGetDecimal(root, "risk", "leverage");
            var maxOpenTrades = TryGetInt(root, "risk", "maxOpenTrades");
            var takeProfitEnabled = TryGetBoolean(root, "exit", "takeProfit", "enabled");
            var stopLossEnabled = TryGetBoolean(root, "exit", "stopLoss", "enabled");
            var takeProfitValue = TryGetDecimal(root, "exit", "takeProfit", "value");
            var stopLossValue = TryGetDecimal(root, "exit", "stopLoss", "value");

            var builder = new StringBuilder();
            builder.AppendLine("## 1. Strategy Summary");
            builder.AppendLine($"- Strategy: **{strategyName}**.");
            builder.AppendLine($"- Type: **{strategyMode}** on **{market} {timeframe}** with **{direction}** bias.");
            builder.AppendLine($"- Entry rules configured: **{entryConditions?.ToString() ?? "0"}**.");
            builder.AppendLine();

            builder.AppendLine("## 2. Entry Logic Quality");
            if (entryConditions is > 0)
            {
                builder.AppendLine($"- The strategy has **{entryConditions}** explicit entry condition(s), which gives it a defined trigger set.");
                builder.AppendLine("- Review whether those conditions are too narrow for live market noise and whether they confirm each other meaningfully.");
            }
            else
            {
                builder.AppendLine("- No explicit entry conditions were found, which makes the live trigger logic hard to assess.");
            }

            builder.AppendLine();
            builder.AppendLine("## 3. Exit Logic Completeness");
            builder.AppendLine($"- Take profit: {(takeProfitEnabled == true ? $"enabled at {takeProfitValue?.ToString() ?? "an unspecified value"}" : "not clearly enabled") }.");
            builder.AppendLine($"- Stop loss: {(stopLossEnabled == true ? $"enabled at {stopLossValue?.ToString() ?? "an unspecified value"}" : "not clearly enabled") }.");
            if (stopLossEnabled != true)
            {
                builder.AppendLine("- Missing or disabled stop loss materially weakens downside protection.");
            }

            builder.AppendLine();
            builder.AppendLine("## 4. Risk Management");
            builder.AppendLine($"- Leverage: **{leverage?.ToString() ?? "not specified"}**.");
            builder.AppendLine($"- Max open trades: **{maxOpenTrades?.ToString() ?? "not specified"}**.");
            if (leverage is > 3)
            {
                builder.AppendLine("- Higher leverage increases execution sensitivity and makes stop placement more important.");
            }
            if (maxOpenTrades is null)
            {
                builder.AppendLine("- No clear cap on concurrent trades was detected in the parsed risk block.");
            }

            builder.AppendLine();
            builder.AppendLine("## 5. Strategy Weaknesses");
            builder.AppendLine("- The current configuration gives a basic structural picture, but it does not fully prove regime fit or live robustness.");
            builder.AppendLine("- The strategy should still be reviewed for regime fit, overfitting risk, and execution realism before deployment.");

            builder.AppendLine();
            builder.AppendLine("## 6. Improvement Suggestions");
            builder.AppendLine("- Re-run the review after confirming the strategy has clear entry, exit, and risk controls.");
            builder.AppendLine("- Add or verify stop loss, position limits, and market-regime assumptions if they are missing.");

            builder.AppendLine();
            builder.AppendLine("Overall assessment: the configuration is readable and internally consistent at a high level, but it still needs deeper qualitative review before it should be trusted in live trading.");

            return builder.ToString().Trim();
        }
        catch (JsonException)
        {
            return "## 1. Strategy Summary\n- The saved strategy configuration could not be summarized cleanly into a structured review.\n\n## 2. Improvement Suggestions\n- Re-run the review after saving the strategy again.\n- Verify the strategy JSON is complete and valid.\n\nOverall assessment: the strategy needs another review pass before it should be relied on for decision-making.";
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetArrayLength(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : null;
    }

    private static decimal? TryGetDecimal(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetDecimal(out var value)
            ? value
            : null;
    }

    private static int? TryGetInt(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? TryGetBoolean(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}