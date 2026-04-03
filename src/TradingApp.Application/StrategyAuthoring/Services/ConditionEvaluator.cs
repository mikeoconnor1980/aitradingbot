using Microsoft.Extensions.Logging;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

/// <summary>
/// Evaluates strategy entry conditions by dispatching to registered condition handlers.
/// </summary>
public interface IConditionEvaluator
{
    ConditionEvaluationResult Evaluate(StrategyConfig config, MarketContext context);
}

public sealed class ConditionEvaluator : IConditionEvaluator
{
    private readonly Dictionary<EntryConditionType, IConditionHandler> _handlers;
    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(IEnumerable<IConditionHandler> handlers, ILogger<ConditionEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = handlers.ToDictionary(handler => handler.ConditionType);
    }

    public ConditionEvaluationResult Evaluate(StrategyConfig config, MarketContext context)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);

        var enabledConditions = config.EntryConditions?
            .Where(condition => condition.Enabled)
            .ToList() ?? [];

        if (enabledConditions.Count == 0)
        {
            return new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "No enabled entry conditions."
            };
        }

        if (context.IndicatorContext is null)
        {
            return new ConditionEvaluationResult
            {
                SetupDetected = false,
                ConditionResults = [],
                OverallReason = "Indicator context not available."
            };
        }

        var results = new List<ConditionResult>();
        var evaluatedResults = new List<ConditionResult>();

        foreach (var condition in enabledConditions)
        {
            if (_handlers.TryGetValue(condition.Type, out var handler))
            {
                var result = handler.Evaluate(condition, context.IndicatorContext, context);
                results.Add(result);
                evaluatedResults.Add(result);
                continue;
            }

            _logger.LogWarning(
                "No handler registered for condition type {ConditionType}. Skipping condition {ConditionId}.",
                condition.Type,
                condition.Id);

            results.Add(new ConditionResult
            {
                ConditionId = condition.Id,
                Passed = true,
                Reason = $"No handler for condition type '{condition.Type}' - skipped."
            });
        }

        var entryLogic = config.EntryLogic ?? EntryLogic.All;

        bool setupDetected;
        string reason;

        if (evaluatedResults.Count == 0)
        {
            setupDetected = true;
            reason = "All conditions skipped (unknown types).";
        }
        else if (entryLogic == EntryLogic.All)
        {
            setupDetected = evaluatedResults.All(result => result.Passed);
            var failedCount = evaluatedResults.Count(result => !result.Passed);
            reason = setupDetected
                ? $"All {evaluatedResults.Count} conditions passed."
                : $"{failedCount}/{evaluatedResults.Count} conditions failed.";
        }
        else
        {
            setupDetected = evaluatedResults.Any(result => result.Passed);
            var passedCount = evaluatedResults.Count(result => result.Passed);
            reason = setupDetected
                ? $"{passedCount}/{evaluatedResults.Count} conditions passed (any mode)."
                : $"No conditions passed out of {evaluatedResults.Count} (any mode).";
        }

        return new ConditionEvaluationResult
        {
            SetupDetected = setupDetected,
            ConditionResults = results,
            OverallReason = reason
        };
    }
}