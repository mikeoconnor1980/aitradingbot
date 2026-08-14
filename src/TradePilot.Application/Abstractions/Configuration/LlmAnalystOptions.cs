using System.ComponentModel.DataAnnotations;

namespace TradePilot.Application.Abstractions.Configuration;

/// <summary>
/// Configures the request-scoped TradePilot Analyst and its bounded tool loop.
/// </summary>
public sealed class LlmAnalystOptions
{
    /// <summary>The configuration section used by the Analyst.</summary>
    public const string SectionName = "LlmAnalyst";

    /// <summary>Gets or sets the maximum number of LLM responses that may request tools.</summary>
    [Range(1, 10)]
    public int MaxToolRounds { get; set; } = 5;

    /// <summary>Gets or sets the maximum number of tool calls requested in one analysis.</summary>
    [Range(1, 20)]
    public int MaxToolCalls { get; set; } = 10;
}
