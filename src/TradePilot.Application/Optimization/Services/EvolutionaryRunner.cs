using Microsoft.Extensions.Logging;
using TradePilot.Application.Optimization.Models;
using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.Optimization.Services;

public sealed class EvolutionaryRunner
{
    private readonly IStrategyConfigGenerator _configGenerator;
    private readonly ILogger _logger;

    public EvolutionaryRunner(IStrategyConfigGenerator configGenerator, ILogger logger)
    {
        _configGenerator = configGenerator;
        _logger = logger;
    }

    public IReadOnlyList<GeneratedStrategy> Breed(
        IReadOnlyList<GeneratedStrategy> elites,
        string symbol,
        ParameterBounds bounds,
        int offspringCount,
        decimal crossoverRate,
        decimal mutationRate,
        int generation)
    {
        ArgumentNullException.ThrowIfNull(elites);
        ArgumentOutOfRangeException.ThrowIfLessThan(elites.Count, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(offspringCount);

        var rng = new Random(generation * 7919);
        var offspring = new List<GeneratedStrategy>(offspringCount);

        // Generate offspring via crossover + mutation
        for (var i = 0; i < offspringCount; i++)
        {
            var parentA = elites[rng.Next(elites.Count)];
            var parentB = elites[rng.Next(elites.Count)];

            // Ensure different parents when possible
            if (elites.Count > 1)
            {
                while (ReferenceEquals(parentA, parentB))
                {
                    parentB = elites[rng.Next(elites.Count)];
                }
            }

            var child = (decimal)rng.NextDouble() < crossoverRate
                ? Crossover(parentA, parentB, rng, generation, i)
                : parentA with { Description = $"Gen{generation}-Clone-{i + 1} | {parentA.Description}" };

            child = (decimal)rng.NextDouble() < mutationRate
                ? Mutate(child, bounds, rng, symbol, generation, i)
                : child;

            offspring.Add(child);
        }

        return offspring;
    }

    private static GeneratedStrategy Crossover(
        GeneratedStrategy parentA,
        GeneratedStrategy parentB,
        Random rng,
        int generation,
        int index)
    {
        var configA = parentA.Config;
        var configB = parentB.Config;

        // Randomly pick exit from one parent and risk from the other
        var useExitFromA = rng.Next(2) == 0;
        var useRiskFromA = rng.Next(2) == 0;
        var useDirectionFromA = rng.Next(2) == 0;
        var useConditionsFromA = rng.Next(2) == 0;
        var useTrendFromA = rng.Next(2) == 0;
        var useTimeframeFromA = rng.Next(2) == 0;

        var exit = useExitFromA ? configA.Exit : configB.Exit;
        var risk = useRiskFromA ? configA.Risk : configB.Risk;
        var direction = useDirectionFromA ? configA.Direction : configB.Direction;
        var entryConditions = useConditionsFromA ? configA.EntryConditions : configB.EntryConditions;
        var entryLogic = useConditionsFromA ? configA.EntryLogic : configB.EntryLogic;
        var trendFilter = useTrendFromA ? configA.TrendFilter : configB.TrendFilter;
        var timeframe = useTimeframeFromA ? configA.Timeframe : configB.Timeframe;

        var childConfig = configA with
        {
            StrategyName = $"Gen{generation}-Cross-{index + 1}",
            Direction = direction,
            Timeframe = timeframe,
            EntryLogic = entryLogic,
            EntryConditions = entryConditions,
            TrendFilter = trendFilter,
            Exit = exit,
            Risk = risk,
        };

        var descParts = new List<string>();
        descParts.Add(useConditionsFromA ? "Conds:A" : "Conds:B");
        descParts.Add(useExitFromA ? "Exit:A" : "Exit:B");
        descParts.Add(useRiskFromA ? "Risk:A" : "Risk:B");

        return new GeneratedStrategy(childConfig, $"Gen{generation}-Cross-{index + 1} [{string.Join(",", descParts)}]");
    }

    private GeneratedStrategy Mutate(
        GeneratedStrategy strategy,
        ParameterBounds bounds,
        Random rng,
        string symbol,
        int generation,
        int index)
    {
        // Generate a fresh random strategy and swap one component
        var mutations = _configGenerator.Generate(symbol, bounds, 1, seed: generation * 1000 + index);
        if (mutations.Count == 0)
        {
            return strategy;
        }

        var donor = mutations[0].Config;
        var config = strategy.Config;

        var mutationType = rng.Next(6);
        var mutationLabel = mutationType switch
        {
            0 => "Exit",
            1 => "Risk",
            2 => "Direction",
            3 => "Conditions",
            4 => "TrendFilter",
            _ => "Timeframe"
        };

        config = mutationType switch
        {
            0 => config with { Exit = donor.Exit },
            1 => config with { Risk = donor.Risk },
            2 => config with { Direction = donor.Direction },
            3 => config with
            {
                EntryConditions = donor.EntryConditions,
                EntryLogic = donor.EntryLogic,
            },
            4 => config with { TrendFilter = donor.TrendFilter },
            _ => config with { Timeframe = donor.Timeframe },
        };

        config = config with { StrategyName = $"Gen{generation}-Mut-{index + 1}" };

        return new GeneratedStrategy(config, $"Gen{generation}-Mut-{index + 1} [Mutated:{mutationLabel}] | {strategy.Description}");
    }
}
