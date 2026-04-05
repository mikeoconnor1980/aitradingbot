using Microsoft.Extensions.Logging.Abstractions;
using TradingApp.Application.Optimization.Models;
using TradingApp.Application.Optimization.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Trading;

namespace TradingApp.Application.Tests.Optimization;

[TestClass]
public sealed class EvolutionaryRunnerTests
{
    private readonly Mock<IStrategyConfigGenerator> _configGenerator = new();

    [TestMethod]
    public void GivenTwoElites_WhenBreed_ThenReturnsRequestedOffspringCount()
    {
        var elites = CreateElites(3);
        SetupMutationGenerator();
        var runner = CreateRunner();

        var offspring = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 10, crossoverRate: 0.7m, mutationRate: 0.3m, generation: 1);

        offspring.Should().HaveCount(10);
    }

    [TestMethod]
    public void GivenElites_WhenBreed_ThenOffspringHaveUniqueNames()
    {
        var elites = CreateElites(4);
        SetupMutationGenerator();
        var runner = CreateRunner();

        var offspring = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 1.0m, mutationRate: 0m, generation: 1);

        offspring.Select(o => o.Config.StrategyName).Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public void GivenZeroCrossoverRate_WhenBreed_ThenOffspringAreClones()
    {
        var elites = CreateElites(2);
        var runner = CreateRunner();

        var offspring = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 0m, mutationRate: 0m, generation: 1);

        offspring.Should().HaveCount(5);
        foreach (var child in offspring)
        {
            child.Description.Should().Contain("Clone");
        }
    }

    [TestMethod]
    public void GivenFullCrossoverRate_WhenBreed_ThenOffspringAreCrossovers()
    {
        var elites = CreateElites(3);
        var runner = CreateRunner();

        var offspring = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 1.0m, mutationRate: 0m, generation: 1);

        offspring.Should().HaveCount(5);
        foreach (var child in offspring)
        {
            child.Description.Should().Contain("Cross");
        }
    }

    [TestMethod]
    public void GivenFullMutationRate_WhenBreed_ThenOffspringAreMutated()
    {
        var elites = CreateElites(2);
        SetupMutationGenerator();
        var runner = CreateRunner();

        var offspring = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 1.0m, mutationRate: 1.0m, generation: 1);

        offspring.Should().HaveCount(5);
        foreach (var child in offspring)
        {
            child.Description.Should().Contain("Mut");
        }
    }

    [TestMethod]
    public void GivenFewerThan2Elites_WhenBreed_ThenThrowsArgumentOutOfRange()
    {
        var elites = CreateElites(1);
        var runner = CreateRunner();

        var act = () => runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 0.7m, mutationRate: 0.3m, generation: 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void GivenNullElites_WhenBreed_ThenThrowsArgumentNull()
    {
        var runner = CreateRunner();

        var act = () => runner.Breed(null!, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 0.7m, mutationRate: 0.3m, generation: 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void GivenSameGeneration_WhenBreedTwice_ThenProducesDeterministicResults()
    {
        var elites = CreateElites(3);
        var runner = CreateRunner();

        var first = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 0.7m, mutationRate: 0m, generation: 1);
        var second = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 0.7m, mutationRate: 0m, generation: 1);

        first.Select(o => o.Config.StrategyName).Should().BeEquivalentTo(second.Select(o => o.Config.StrategyName));
    }

    [TestMethod]
    public void GivenDifferentGenerations_WhenBreed_ThenProducesDifferentSeeds()
    {
        var elites = CreateElites(5);
        var runner = CreateRunner();

        var gen1 = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 1.0m, mutationRate: 0m, generation: 1);
        var gen2 = runner.Breed(elites, "BTC", new ParameterBounds(), offspringCount: 5, crossoverRate: 1.0m, mutationRate: 0m, generation: 2);

        gen1.Select(o => o.Config.StrategyName).Should().NotBeEquivalentTo(gen2.Select(o => o.Config.StrategyName));
    }

    private EvolutionaryRunner CreateRunner()
    {
        return new EvolutionaryRunner(_configGenerator.Object, NullLogger.Instance);
    }

    private void SetupMutationGenerator()
    {
        _configGenerator
            .Setup(g => g.Generate(It.IsAny<string>(), It.IsAny<ParameterBounds>(), 1, It.IsAny<int?>()))
            .Returns((string _, ParameterBounds _, int _, int? seed) =>
            {
                var config = CreateStrategyConfig($"Donor-{seed}");
                return new List<GeneratedStrategy> { new(config, $"Donor {seed}") };
            });
    }

    private static IReadOnlyList<GeneratedStrategy> CreateElites(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new GeneratedStrategy(CreateStrategyConfig($"Elite-{i}"), $"Elite {i}"))
            .ToList();
    }

    private static StrategyConfig CreateStrategyConfig(string name)
    {
        return new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Signal,
            StrategyName = name,
            Exchange = "Hyperliquid",
            Market = "BTC",
            Timeframe = "15m",
            Direction = Direction.Long,
            Enabled = true,
            EntryLogic = EntryLogic.All,
            EntryConditions =
            [
                new EntryConditionConfig
                {
                    Id = $"rsi-{name}",
                    Enabled = true,
                    Type = EntryConditionType.Rsi,
                    Label = "RSI",
                    Params = new RsiParams
                    {
                        Period = 14,
                        Operator = "lt",
                        Value = 40m,
                    },
                },
            ],
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 4m },
                StopLoss = new ExitRuleConfig { Enabled = true, Type = ExitRuleType.FixedPercent, Value = 2m },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 10m,
                Leverage = 3m,
                MaxOpenTrades = 1,
                CooldownValue = 1,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
        };
    }
}
