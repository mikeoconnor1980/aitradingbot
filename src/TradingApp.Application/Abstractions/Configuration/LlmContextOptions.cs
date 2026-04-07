using System.ComponentModel.DataAnnotations;

namespace TradingApp.Application.Abstractions.Configuration;

public sealed class LlmContextOptions
{
    public const string SectionName = "LlmContext";

    [Required]
    public string Provider { get; set; } = "Gemini";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [Required]
    public string ModelName { get; set; } = "gemini-2.5-flash-lite";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// How long a cached LLM context result remains valid before a fresh call is made.
    /// </summary>
    [Range(1, 7200)]
    public int CacheDurationSeconds { get; set; } = 3600; // 60 minutes
}
