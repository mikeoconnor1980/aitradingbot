namespace TradePilot.Application.StrategyAuthoring.Models;

public sealed class StrategyDiffDto
{
    public int FromRevision { get; init; }
    public int ToRevision { get; init; }
    public IReadOnlyList<FieldChangeDto> Changes { get; init; } = [];
}