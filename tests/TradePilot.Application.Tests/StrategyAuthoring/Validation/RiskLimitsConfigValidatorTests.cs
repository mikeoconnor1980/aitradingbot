using Microsoft.Extensions.Options;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Validation;

namespace TradePilot.Application.Tests.StrategyAuthoring.Validation;

[TestClass]
public sealed class RiskLimitsConfigValidatorTests
{
    private RiskLimitsConfigValidator _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new RiskLimitsConfigValidator();
    }

    [TestMethod]
    public void GivenDefaultTiers_WhenValidated_ThenSucceeds()
    {
        var config = new RiskLimitsConfig();
        RiskLimitsConfigDefaults.Apply(config);

        var result = _sut.Validate(null, config);

        result.Should().Be(ValidateOptionsResult.Success);
    }

    [TestMethod]
    public void GivenEmptyTiers_WhenValidated_ThenSucceeds()
    {
        var config = new RiskLimitsConfig { DrawdownTiers = [] };

        var result = _sut.Validate(null, config);

        result.Should().Be(ValidateOptionsResult.Success);
    }

    [TestMethod]
    public void GivenThresholdsNotAscending_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers =
            [
                new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.75m },
                new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.50m },
            ]
        };

        var result = _sut.Validate(null, config);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ascending");
    }

    [TestMethod]
    public void GivenScalingFactorAboveOne_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers = [new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 1.5m }]
        };

        var result = _sut.Validate(null, config);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("between 0.0 and 1.0");
    }

    [TestMethod]
    public void GivenScalingFactorsNotDescending_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers =
            [
                new DrawdownTier { ThresholdPercent = 5m, ScalingFactor = 0.50m },
                new DrawdownTier { ThresholdPercent = 10m, ScalingFactor = 0.75m },
            ]
        };

        var result = _sut.Validate(null, config);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("descending");
    }

    [TestMethod]
    public void GivenNegativeThreshold_WhenValidated_ThenFails()
    {
        var config = new RiskLimitsConfig
        {
            DrawdownTiers = [new DrawdownTier { ThresholdPercent = -1m, ScalingFactor = 0.75m }]
        };

        var result = _sut.Validate(null, config);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("greater than 0");
    }
}