namespace TradingApp.Application.Optimization.Models;

public sealed record EvolutionaryConfig
{
    public bool Enabled { get; init; }
    public int Generations { get; init; } = 5;
    public int EliteCount { get; init; } = 10;
    public decimal MutationRate { get; init; } = 0.3m;
    public decimal CrossoverRate { get; init; } = 0.7m;
}
