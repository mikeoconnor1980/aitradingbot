namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class StrategyIntentDto
{
    public StrategyConfig Config { get; init; } = new();
    public decimal Confidence { get; init; }
    public IReadOnlyList<AssumptionDto> Assumptions { get; init; } = [];
    public string? ClarificationNeeded { get; init; }
}