namespace TradingApp.Application.Optimization.Models;

public sealed class OptimizationRunResponse
{
    public required Guid Id { get; init; }
    public required string Symbol { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required decimal InitialCapital { get; init; }
    public required string Status { get; init; }
    public required int TotalCombinations { get; init; }
    public required int CompletedCount { get; init; }
    public required int QualifiedCount { get; init; }
    public required long ElapsedMs { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyList<OptimizationResultResponse> Results { get; init; }
}