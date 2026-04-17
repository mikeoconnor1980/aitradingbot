namespace TradePilot.Application.Optimization.Models;

public sealed class OptimizationRunSummary
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required string Status { get; init; }
    public required int TotalCombinations { get; init; }
    public required int CompletedCount { get; init; }
    public required int QualifiedCount { get; init; }
    public required int FailedCount { get; init; }
    public required long ElapsedMs { get; init; }
    public required DateTime CreatedAt { get; init; }
    public decimal? TopFitnessScore { get; init; }
    public decimal? TopTotalPnl { get; init; }
    public decimal? TopWinRate { get; init; }
    public string? TopSignalDescription { get; init; }
}