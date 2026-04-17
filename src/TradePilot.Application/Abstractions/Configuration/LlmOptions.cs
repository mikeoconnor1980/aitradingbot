using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    [Required]
    public string Provider { get; set; } = "Gemini";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [Required]
    public string ModelName { get; set; } = "gemini-2.0-flash";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 30;
}