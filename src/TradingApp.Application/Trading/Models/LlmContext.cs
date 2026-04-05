namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Qualitative market context produced by an LLM or synthetic regime provider.
/// Influences strategy behaviour (grid sizing, position sizing, entry gating)
/// but never places trades directly.
/// </summary>
public sealed class LlmContext
{
    public required string MarketSentiment { get; init; }
    public required string MacroRegime { get; init; }
    public required string EventRisk { get; init; }
    public required decimal Confidence { get; init; }
    public required MarketRegime DerivedRegime { get; init; }
    public string Summary { get; init; } = string.Empty;
    public required long GeneratedAtUtc { get; init; }
}
