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
    public void GivenDefaultBounds_WhenGenerate_ThenAllConfigsAreSignalMode()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 30, seed: 456);

        results.Should().OnlyContain(strategy =>
            strategy.Config.StrategyMode == StrategyMode.Signal
            && (strategy.Config.Direction == Direction.Long || strategy.Config.Direction == Direction.Short)
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
    public void GivenDefaultStopLossTypes_WhenGenerate_ThenAllConfigsUseFixedPercentStopLoss()
    {
        var results = _generator.Generate("BTC", new ParameterBounds(), 25, seed: 1010);

        results.Should().OnlyContain(strategy => strategy.Config.Exit.StopLoss.Type == ExitRuleType.FixedPercent);
    }

    [TestMethod]
    public void GivenAtrInitialStopLossType_WhenGenerate_ThenConfigUsesAtrOptions()
    {
        decimal[] atrMultiplierOptions = [1.0m, 1.5m, 2.0m, 2.5m, 3.0m];
        int[] atrPeriodOptions = [14, 21];
        var bounds = new ParameterBounds
        {
            StopLossTypes = [ExitRuleType.AtrInitial],
            AtrMultiplierOptions = atrMultiplierOptions,
            AtrPeriodOptions = atrPeriodOptions,
        };

        var results = _generator.Generate("BTC", bounds, 25, seed: 1011);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Exit.StopLoss.Type == ExitRuleType.AtrInitial
            && strategy.Config.Exit.StopLoss.AtrMultiplier.HasValue
            && atrMultiplierOptions.Contains(strategy.Config.Exit.StopLoss.AtrMultiplier.Value)
            && strategy.Config.Exit.StopLoss.AtrPeriod.HasValue
            && atrPeriodOptions.Contains(strategy.Config.Exit.StopLoss.AtrPeriod.Value)
            && strategy.Config.Exit.StopLoss.Value == null);
        results.Should().OnlyContain(strategy => strategy.Description.Contains("SL:ATRx", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GivenMixedStopLossTypes_WhenGenerate_ThenConfigsContainBothSupportedTypes()
    {
        var bounds = new ParameterBounds
        {
            StopLossTypes = [ExitRuleType.FixedPercent, ExitRuleType.AtrInitial],
            AtrMultiplierOptions = [2.0m],
            AtrPeriodOptions = [14],
        };

        var results = _generator.Generate("BTC", bounds, 200, seed: 1012);

        results.Should().Contain(strategy => strategy.Config.Exit.StopLoss.Type == ExitRuleType.FixedPercent);
        results.Should().Contain(strategy => strategy.Config.Exit.StopLoss.Type == ExitRuleType.AtrInitial);
    }

    [TestMethod]
    public void GivenAtrInitialWithoutMultiplierOptions_WhenGenerate_ThenThrows()
    {
        var bounds = new ParameterBounds
        {
            StopLossTypes = [ExitRuleType.AtrInitial],
            AtrMultiplierOptions = [],
        };

        var action = () => _generator.Generate("BTC", bounds, 10, seed: 1013);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ATR multiplier*");
    }

    [TestMethod]
    public void GivenAtrInitialWithoutPeriodOptions_WhenGenerate_ThenThrows()
    {
        var bounds = new ParameterBounds
        {
            StopLossTypes = [ExitRuleType.AtrInitial],
            AtrPeriodOptions = [],
        };

        var action = () => _generator.Generate("BTC", bounds, 10, seed: 1014);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ATR period*");
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

    [TestMethod]
    public void GivenMultipleTimeframes_WhenGenerate_ThenStrategiesUseProvidedTimeframes()
    {
        var bounds = new ParameterBounds { Timeframes = ["5m", "15m", "1h"] };
        var results = _generator.Generate("BTC", bounds, 100, seed: 3030);

        var timeframes = results.Select(s => s.Config.Timeframe).Distinct().ToArray();

        timeframes.Should().BeSubsetOf(["5m", "15m", "1h"]);
        timeframes.Should().HaveCountGreaterThan(1, "with 100 samples across 3 timeframes, multiple should appear");
    }

    [TestMethod]
    public void GivenSingleTimeframe_WhenGenerate_ThenAllStrategiesUseThatTimeframe()
    {
        var bounds = new ParameterBounds { Timeframes = ["4h"] };
        var results = _generator.Generate("BTC", bounds, 20, seed: 4040);

        results.Should().OnlyContain(s => s.Config.Timeframe == "4h");
    }

    [TestMethod]
    public void GivenRiskBasedMode_WhenGenerate_ThenAllCandidatesUseRiskBasedSizing()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            RiskPerTradePercentOptions = [0.5m, 1.0m, 1.5m, 2.0m],
        };

        var results = _generator.Generate("BTC", bounds, 50, seed: 5000);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Risk.PositionSizeType == PositionSizeType.RiskBased
            && strategy.Config.Risk.RiskPerTradePercent > 0m);
    }

    [TestMethod]
    public void GivenRiskBasedMode_WhenGenerate_ThenRiskPerTradePercentDrawnFromOptions()
    {
        decimal[] options = [0.5m, 1.0m, 1.5m, 2.0m];
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            RiskPerTradePercentOptions = options,
        };

        var results = _generator.Generate("BTC", bounds, 100, seed: 5001);

        results.Should().OnlyContain(strategy => options.Contains(strategy.Config.Risk.RiskPerTradePercent.GetValueOrDefault()));
    }

    [TestMethod]
    public void GivenPercentWalletMode_WhenGenerate_ThenAllCandidatesUsePercentWallet()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.PercentWallet,
        };

        var results = _generator.Generate("BTC", bounds, 30, seed: 5002);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Risk.PositionSizeType == PositionSizeType.PercentWallet
            && strategy.Config.Risk.PositionSizeValue > 0m);
    }

    [TestMethod]
    public void GivenRiskBasedWithAutoLeverageTrue_WhenGenerate_ThenLeverageNotSwept()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            IncludeAutoLeverage = true,
            LeverageMin = 3m,
            LeverageMax = 10m,
            RiskPerTradePercentOptions = [1.0m],
        };

        var results = _generator.Generate("BTC", bounds, 200, seed: 5003);
        var autoLeverageCandidates = results.Where(strategy => strategy.Config.Risk.AutoLeverage).ToList();

        autoLeverageCandidates.Should().NotBeEmpty();
        autoLeverageCandidates.Should().OnlyContain(strategy => strategy.Config.Risk.Leverage == 1m);
    }

    [TestMethod]
    public void GivenRiskBasedWithAutoLeverageFalse_WhenGenerate_ThenLeverageSwept()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            IncludeAutoLeverage = false,
            LeverageMin = 3m,
            LeverageMax = 10m,
            RiskPerTradePercentOptions = [1.0m],
        };

        var results = _generator.Generate("BTC", bounds, 50, seed: 5004);

        results.Should().OnlyContain(strategy =>
            strategy.Config.Risk.AutoLeverage == false
            && strategy.Config.Risk.Leverage >= bounds.LeverageMin
            && strategy.Config.Risk.Leverage <= bounds.LeverageMax);
    }

    [TestMethod]
    public void GivenRiskBasedWithIncludeAutoLeverage_WhenGenerate_ThenBothVariantsPresent()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            IncludeAutoLeverage = true,
        };

        var results = _generator.Generate("BTC", bounds, 200, seed: 5005);

        results.Should().Contain(strategy => strategy.Config.Risk.AutoLeverage);
        results.Should().Contain(strategy => !strategy.Config.Risk.AutoLeverage);
    }

    [TestMethod]
    public void GivenIncludeAutoLeverageFalse_WhenGenerate_ThenAllAutoLeverageFalse()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            IncludeAutoLeverage = false,
        };

        var results = _generator.Generate("BTC", bounds, 50, seed: 5006);

        results.Should().OnlyContain(strategy => !strategy.Config.Risk.AutoLeverage);
    }

    [TestMethod]
    public void GivenRiskBasedModeWithEmptyOptions_WhenGenerate_ThenThrows()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            RiskPerTradePercentOptions = [],
        };

        var action = () => _generator.Generate("BTC", bounds, 10, seed: 5007);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*RiskPerTradePercent*");
    }

    [TestMethod]
    public void GivenPercentWalletModeWithEmptyOptions_WhenGenerate_ThenThrows()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.PercentWallet,
            PositionSizeOptions = [],
        };

        var action = () => _generator.Generate("BTC", bounds, 10, seed: 5010);

        action.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void GivenRiskBasedMode_WhenGenerate_ThenDescriptionsContainRiskPercent()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            RiskPerTradePercentOptions = [1.0m],
            IncludeAutoLeverage = false,
        };

        var results = _generator.Generate("BTC", bounds, 10, seed: 5008);

        results.Should().OnlyContain(strategy => strategy.Description.Contains("R:1%/trade", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GivenRiskBasedModeWithAutoLeverage_WhenGenerate_ThenDescriptionsContainAutoLev()
    {
        var bounds = new ParameterBounds
        {
            PositionSizeMode = PositionSizeMode.RiskBased,
            IncludeAutoLeverage = true,
        };

        var results = _generator.Generate("BTC", bounds, 200, seed: 5009);
        var autoLevResults = results.Where(strategy => strategy.Config.Risk.AutoLeverage).ToList();

        autoLevResults.Should().NotBeEmpty();
        autoLevResults.Should().OnlyContain(strategy => strategy.Description.Contains("AutoLev", StringComparison.Ordinal));
    }
}