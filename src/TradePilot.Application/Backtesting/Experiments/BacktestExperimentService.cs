using System.Text.Json;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Backtesting.Experiments;

public sealed class BacktestExperimentService : IBacktestExperimentService
{
    public const int MaximumCandidates = 5;
    public const int MaximumDateRangeDays = 366;
    public const string RsiValueParameter = "rsi.value";

    private static readonly string[] RequiredIntervals = ["15m", "1h", "4h"];

    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyRevisionRepository _revisionRepository;
    private readonly IBacktestRunner _backtestRunner;

    public BacktestExperimentService(
        IStrategyRepository strategyRepository,
        IStrategyRevisionRepository revisionRepository,
        IBacktestRunner backtestRunner)
    {
        _strategyRepository = strategyRepository;
        _revisionRepository = revisionRepository;
        _backtestRunner = backtestRunner;
    }

    public async Task<BacktestExperimentResult> RunAsync(
        BacktestExperimentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Strategy), request.StrategyId);
        if (!strategy.IsActive || !string.Equals(strategy.UserId, request.UserId, StringComparison.Ordinal))
        {
            throw new NotFoundException(nameof(Strategy), request.StrategyId);
        }

        var (baseConfig, baseVersion) = await GetBaseConfigAsync(strategy, request.StrategyVersion, cancellationToken);
        var settings = request.Settings ?? new BacktestExperimentSettings();
        var candidateConfigs = request.Candidates
            .Select(candidate => (Candidate: candidate, Config: ApplyOverrides(baseConfig, candidate.ConfigurationOverrides)))
            .ToArray();
        var baselineResult = await _backtestRunner.RunAsync(
            CreateBacktestConfig(request, baseConfig, settings), cancellationToken);
        var baseline = BacktestExperimentMetrics.From(baselineResult);
        var candidates = new List<BacktestCandidateExperimentResult>(candidateConfigs.Length);

        foreach (var candidate in candidateConfigs)
        {
            var candidateResult = await _backtestRunner.RunAsync(
                CreateBacktestConfig(request, candidate.Config, settings), cancellationToken);
            var metrics = BacktestExperimentMetrics.From(candidateResult);
            candidates.Add(new BacktestCandidateExperimentResult(
                candidate.Candidate.Label,
                candidate.Candidate.ConfigurationOverrides,
                metrics,
                BacktestComparison.Between(baseline, metrics)));
        }

        return new BacktestExperimentResult(
            strategy.Id,
            baseVersion,
            request.Symbol,
            request.Start,
            request.End,
            request.InitialCapital,
            baseline,
            candidates);
    }

    private async Task<(StrategyConfig Config, int Version)> GetBaseConfigAsync(
        Strategy strategy,
        int? requestedVersion,
        CancellationToken cancellationToken)
    {
        var version = requestedVersion ?? await _revisionRepository.GetLatestRevisionNumberAsync(strategy.Id, cancellationToken);
        var json = strategy.ConfigJson;
        if (version > 0)
        {
            var revision = await _revisionRepository.GetByStrategyAndRevisionAsync(strategy.Id, version, cancellationToken)
                ?? throw new NotFoundException(nameof(StrategyRevision), version);
            json = revision.ConfigJson;
        }

        var config = JsonSerializer.Deserialize<StrategyConfig>(json, StrategyJsonOptions.Default)
            ?? throw new DomainException("The selected strategy version could not be read as a strategy configuration.");
        return (config, version > 0 ? version : strategy.Version);
    }

    private static BacktestConfig CreateBacktestConfig(
        BacktestExperimentRequest request,
        StrategyConfig strategy,
        BacktestExperimentSettings settings) => new()
    {
        Symbol = request.Symbol,
        Intervals = RequiredIntervals,
        StartDateUtc = request.Start.ToUnixTimeMilliseconds(),
        EndDateUtc = request.End.ToUnixTimeMilliseconds(),
        InitialCapital = request.InitialCapital,
        Strategy = strategy,
        Execution = new ExecutionConfig(),
        TriggerTimeframe = strategy.Timeframe,
        WarmupPeriod = settings.WarmupPeriod,
        EnableAuditLog = settings.EnableAuditLog,
    };

    private static StrategyConfig ApplyOverrides(
        StrategyConfig baseConfig,
        IReadOnlyList<StrategyParameterOverride> overrides)
    {
        var conditions = baseConfig.EntryConditions?.ToArray()
            ?? throw new DomainException("The selected strategy has no configurable entry conditions.");

        foreach (var parameterOverride in overrides)
        {
            if (!string.Equals(parameterOverride.Parameter, RsiValueParameter, StringComparison.Ordinal))
            {
                throw new DomainException($"'{parameterOverride.Parameter}' is not an experiment-configurable parameter.");
            }

            if (parameterOverride.Value is < 0 or > 100)
            {
                throw new DomainException("RSI values must be between 0 and 100.");
            }

            var index = Array.FindIndex(conditions, condition =>
                string.Equals(condition.Id, parameterOverride.ConditionId, StringComparison.Ordinal));
            if (index < 0 || conditions[index].Type != EntryConditionType.Rsi || conditions[index].Params is not RsiParams rsi)
            {
                throw new DomainException("The requested RSI condition was not found or is not configurable.");
            }

            conditions[index] = conditions[index] with { Params = rsi with { Value = parameterOverride.Value } };
        }

        return baseConfig with { EntryConditions = conditions };
    }

    private static void ValidateRequest(BacktestExperimentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Symbol);
        ArgumentNullException.ThrowIfNull(request.Candidates);
        if (request.StrategyId == Guid.Empty)
        {
            throw new ArgumentException("A base strategy is required.", nameof(request));
        }

        if (request.Start >= request.End || request.End - request.Start > TimeSpan.FromDays(MaximumDateRangeDays))
        {
            throw new DomainException($"Experiment periods must be positive and no longer than {MaximumDateRangeDays} days.");
        }

        if (request.InitialCapital <= 0)
        {
            throw new DomainException("Initial capital must be greater than zero.");
        }

        if (request.Candidates.Count is < 1 or > MaximumCandidates)
        {
            throw new DomainException($"Experiments must contain between one and {MaximumCandidates} candidates.");
        }

        if (request.RegimeFilter is not null)
        {
            throw new DomainException("Regime-filtered experiments are not supported by the deterministic replay engine yet.");
        }

        var settings = request.Settings;
        if (settings is not null && settings.WarmupPeriod is < 1 or > 1_000)
        {
            throw new DomainException("Warmup periods must be between 1 and 1000 candles.");
        }

        var candidateKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in request.Candidates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.Label);
            ArgumentNullException.ThrowIfNull(candidate.ConfigurationOverrides);
            if (candidate.ConfigurationOverrides.Count != 1)
            {
                throw new DomainException("Each experiment candidate must change exactly one supported parameter.");
            }

            var parameterOverride = candidate.ConfigurationOverrides[0];
            ArgumentException.ThrowIfNullOrWhiteSpace(parameterOverride.Parameter);
            ArgumentException.ThrowIfNullOrWhiteSpace(parameterOverride.ConditionId);
            var key = $"{parameterOverride.Parameter}:{parameterOverride.ConditionId}:{parameterOverride.Value}";
            if (!candidateKeys.Add(key))
            {
                throw new DomainException("Experiment candidates must not duplicate the same configuration override.");
            }
        }
    }
}