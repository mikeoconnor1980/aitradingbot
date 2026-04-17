namespace TradePilot.Application.Optimization.Models;

public sealed record SweepConfig
{
    public required string Symbol { get; init; }
    public string? BacktestSymbol { get; init; }
    public required long StartDateUtc { get; init; }
    public required long EndDateUtc { get; init; }
    public required decimal InitialCapital { get; init; }
    public int SampleSize { get; init; } = 500;
    public int MaxDegreeOfParallelism { get; init; }
    public ParameterBounds Bounds { get; init; } = new();
    public FitnessThresholds Thresholds { get; init; } = new();
    public WalkForwardConfig WalkForward { get; init; } = new();
    public EvolutionaryConfig Evolutionary { get; init; } = new();
}