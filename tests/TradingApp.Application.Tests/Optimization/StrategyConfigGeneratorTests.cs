using System.Text.Json;
using TradingApp.Application.Optimization.Models;
using TradingApp.Application.Optimization.Services;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;

namespace TradingApp.Application.Tests.Optimization;

[TestClass]
public sealed class StrategyConfigGeneratorTests
{
    private readonly StrategyConfigGenerator _generator = new();

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenReturnsRequestedSampleSize()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 25, seed: 123);

        results.Should().HaveCount(25);
    }

    [TestMethod]
    public void GivenSeed_WhenGenerateTwice_ThenReturnsSameConfigs()
    {
        var first = _generator.Generate("BTC", new ParameterBounds(), 20, seed: 321);
        var second = _generator.Generate("BTC", new ParameterBounds(), 20, seed: 321);

        var firstJson = first.Select(strategy => JsonSerializer.Serialize(strategy.Config, StrategyJsonOptions.Default)).ToArray();
        var secondJson = second.Select(strategy => JsonSerializer.Serialize(strategy.Config, StrategyJsonOptions.Default)).ToArray();
        var firstDescriptions = first.Select(strategy => strategy.Description).ToArray();
        var secondDescriptions = second.Select(strategy => strategy.Description).ToArray();

        firstJson.Should().Equal(secondJson);
        firstDescriptions.Should().Equal(secondDescriptions);
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenAllConfigsAreSignalModeLong()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 30, seed: 456);

        results.Should().OnlyContain(strategy =>
            strategy.Config.StrategyMode == StrategyMode.Signal
            && strategy.Config.Direction == Direction.Long
            && strategy.Config.Market == "BTC");
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenEntryConditionsArePopulated()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 30, seed: 789);

        results.Select(strategy => strategy.Config.EntryConditions).Should().NotContainNulls();
        results.Select(strategy => strategy.Config.EntryConditions!).Should().OnlyContain(conditions =>
            conditions.Count > 0
            && conditions.All(condition => condition.Enabled));
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenStopLossWithinBounds()
    {
        var bounds = new ParameterBounds();
        var results = _generator.Generate("BTC", bounds, 40, seed: 999);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Exit.StopLoss.Value >= bounds.StopLossMin
            && strategy.Config.Exit.StopLoss.Value <= bounds.StopLossMax);
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenTakeProfitWithinBounds()
    {
        var bounds = new ParameterBounds();
        var results = _generator.Generate("BTC", bounds, 40, seed: 1000);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Exit.TakeProfit.Value >= bounds.TakeProfitMin
            && strategy.Config.Exit.TakeProfit.Value <= bounds.TakeProfitMax);
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenLeverageWithinBounds()
    {
        var bounds = new ParameterBounds();
        var results = _generator.Generate("BTC", bounds, 40, seed: 1001);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Risk.Leverage >= bounds.LeverageMin
            && strategy.Config.Risk.Leverage <= bounds.LeverageMax);
    }

    [TestMethod]
    public void GivenDefaultBounds_WhenGenerate_ThenDescriptionsNotEmpty()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 20, seed: 2026);

        results.Should().OnlyContain(strategy => !string.IsNullOrWhiteSpace(strategy.Description));
    }

    [TestMethod]
    public void GivenLargeSample_WhenGenerate_ThenMultipleConditionTypesPresent()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 200, seed: 222);

        var conditionTypes = results
            .SelectMany(strategy => strategy.Config.EntryConditions ?? [])
            .Select(condition => condition.Type)
            .Distinct()
            .ToArray();

        conditionTypes.Should().Contain([EntryConditionType.Rsi, EntryConditionType.Macd, EntryConditionType.PriceVsEma]);
        results.Select(strategy => strategy.Config.EntryConditions?.Count ?? 0).Should().Contain(count => count > 1);
    }
}