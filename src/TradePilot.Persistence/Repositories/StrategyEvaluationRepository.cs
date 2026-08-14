using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyEvaluations.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Persistence.Repositories;

/// <summary>EF Core persistence for high-volume strategy-evaluation evidence.</summary>
public sealed class StrategyEvaluationRepository : IStrategyEvaluationRepository
{
    private readonly TradePilotDbContext _context;
    private readonly ILogger<StrategyEvaluationRepository> _logger;

    public StrategyEvaluationRepository(
        TradePilotDbContext context,
        ILogger<StrategyEvaluationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(StrategyEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        await _context.StrategyEvaluations.AddAsync(evaluation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Persisted strategy evaluation {EvaluationId} for {StrategyName}/{Symbol} with decision {Decision}",
            evaluation.Id,
            evaluation.StrategyName,
            evaluation.Symbol,
            evaluation.Decision);
    }

    public async Task<IReadOnlyList<StrategyEvaluation>> GetAsync(
        StrategyEvaluationFilter filter,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var stopwatch = Stopwatch.StartNew();
        var result = await ApplyFilter(_context.StrategyEvaluations.AsNoTracking(), filter)
            .Include(evaluation => evaluation.Rules)
            .OrderByDescending(evaluation => evaluation.EvaluatedAtUtc)
            .ThenByDescending(evaluation => evaluation.Id)
            .Take(limit)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        _logger.LogInformation(
            "Strategy evaluation history query returned {Count} records in {DurationMs}ms",
            result.Count,
            stopwatch.ElapsedMilliseconds);
        return result;
    }

    public async Task<StrategyEvaluation?> GetLatestAsync(
        StrategyEvaluationFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var stopwatch = Stopwatch.StartNew();
        var result = await ApplyFilter(_context.StrategyEvaluations.AsNoTracking(), filter)
            .Include(evaluation => evaluation.Rules)
            .OrderByDescending(evaluation => evaluation.EvaluatedAtUtc)
            .ThenByDescending(evaluation => evaluation.Id)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
        _logger.LogInformation(
            "Latest strategy evaluation query completed in {DurationMs}ms; Found={Found}",
            stopwatch.ElapsedMilliseconds,
            result is not null);
        return result;
    }

    public async Task<StrategyEvaluationSummary> GetSummaryAsync(
        StrategyEvaluationFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var stopwatch = Stopwatch.StartNew();
        var evaluations = ApplyFilter(_context.StrategyEvaluations.AsNoTracking(), filter);
        var evaluationIds = evaluations.Select(evaluation => evaluation.Id);

        var total = await evaluations.CountAsync(cancellationToken);
        var candidate = await evaluations.CountAsync(evaluation => evaluation.SetupDetected, cancellationToken);
        var tradeDecisions = await evaluations.CountAsync(
            evaluation => evaluation.Decision == StrategyDecision.EnterLong
                || evaluation.Decision == StrategyDecision.EnterShort
                || evaluation.Decision == StrategyDecision.Exit,
            cancellationToken);
        var noTrade = await evaluations.CountAsync(
            evaluation => evaluation.Decision == StrategyDecision.NoTrade,
            cancellationToken);
        var riskRejected = await evaluations.CountAsync(
            evaluation => evaluation.Decision == StrategyDecision.RejectedByRisk,
            cancellationToken);

        var failureCounts = await _context.RuleEvaluations
            .AsNoTracking()
            .Where(rule => evaluationIds.Contains(rule.StrategyEvaluationId) && !rule.Passed && rule.IsBlocking)
            .GroupBy(rule => rule.RuleId)
            .Select(group => new RuleFailureCount(group.Key, group.Min(rule => rule.Name), group.Count()))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.RuleId)
            .ToListAsync(cancellationToken);

        var summary = new StrategyEvaluationSummary(
            total,
            candidate,
            tradeDecisions,
            noTrade,
            riskRejected,
            failureCounts,
            failureCounts.FirstOrDefault());
        _logger.LogInformation(
            "Strategy evaluation summary query aggregated {Count} evaluations in {DurationMs}ms",
            total,
            stopwatch.ElapsedMilliseconds);
        return summary;
    }

    private static IQueryable<StrategyEvaluation> ApplyFilter(
        IQueryable<StrategyEvaluation> query,
        StrategyEvaluationFilter filter)
    {
        if (filter.StrategyId.HasValue)
        {
            query = query.Where(evaluation => evaluation.StrategyId == filter.StrategyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.StrategyName))
        {
            var strategyName = filter.StrategyName.Trim();
            query = query.Where(evaluation => evaluation.StrategyName == strategyName);
        }

        if (filter.StrategyVersion.HasValue)
        {
            query = query.Where(evaluation => evaluation.StrategyVersion == filter.StrategyVersion.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            var symbol = filter.Symbol.Trim().ToUpperInvariant();
            query = query.Where(evaluation => evaluation.Symbol == symbol);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(evaluation => evaluation.EvaluatedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(evaluation => evaluation.EvaluatedAtUtc <= filter.ToUtc.Value);
        }

        return query;
    }
}
