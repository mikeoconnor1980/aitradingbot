using TradingApp.Application.Optimization.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Optimization;

internal static class OptimizationRunResponseMapper
{
    public static OptimizationRunResponse ToResponse(
        OptimizationRun run,
        IReadOnlyList<OptimizationResult>? results = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new OptimizationRunResponse
        {
            Id = run.Id,
            Symbol = run.Symbol,
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(run.StartDateUtc).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(run.EndDateUtc).UtcDateTime,
            InitialCapital = run.InitialCapital,
            Status = run.Status.ToString(),
            TotalCombinations = run.TotalCombinations,
            CompletedCount = run.CompletedCount,
            QualifiedCount = run.QualifiedCount,
            ElapsedMs = run.ElapsedMs,
            ErrorMessage = run.ErrorMessage,
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(run.CreatedAtUtc).UtcDateTime,
            Results = (results ?? []).OrderBy(result => result.Rank).Select(ToResponse).ToList(),
        };
    }

    public static OptimizationResultResponse ToResponse(OptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new OptimizationResultResponse
        {
            Rank = result.Rank,
            FitnessScore = result.FitnessScore,
            SignalDescription = result.SignalDescription,
            StrategyConfigJson = result.StrategyConfigJson,
            TotalPnl = result.TotalPnl,
            WinRate = result.WinRate,
            MaxDrawdown = result.MaxDrawdown,
            TotalTrades = result.TotalTrades,
            WinningTrades = result.WinningTrades,
            LosingTrades = result.LosingTrades,
            TotalFeesPaid = result.TotalFeesPaid,
            AverageTradePnl = result.AverageTradePnl,
            AverageHoldTimeMinutes = result.AverageHoldTimeMinutes,
        };
    }
}