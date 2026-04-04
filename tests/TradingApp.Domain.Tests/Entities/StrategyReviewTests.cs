using TradingApp.Domain.Entities;

namespace TradingApp.Domain.Tests.Entities;

[TestClass]
public sealed class StrategyReviewTests
{
    [TestMethod]
    public void GivenValidInputs_WhenCreate_ThenPropertiesSet()
    {
        var strategyId = Guid.NewGuid();

        var review = StrategyReview.Create(
            strategyId,
            1,
            "## Review\n- Looks good",
            "gemini-2.5-flash-lite");

        review.Id.Should().NotBeEmpty();
        review.StrategyId.Should().Be(strategyId);
        review.RevisionNumber.Should().Be(1);
        review.ReviewMarkdown.Should().Be("## Review\n- Looks good");
        review.ModelName.Should().Be("gemini-2.5-flash-lite");
        review.CreatedAtUtc.Should().BePositive();
    }

    [TestMethod]
    public void GivenEmptyStrategyId_WhenCreate_ThenThrowsArgumentException()
    {
        var act = () => StrategyReview.Create(Guid.Empty, 1, "review", "model");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void GivenInvalidRevisionNumber_WhenCreate_ThenThrowsArgumentOutOfRangeException(int revisionNumber)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), revisionNumber, "review", "model");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GivenInvalidReviewMarkdown_WhenCreate_ThenThrowsArgumentException(string? reviewMarkdown)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), 1, reviewMarkdown!, "model");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GivenInvalidModelName_WhenCreate_ThenThrowsArgumentException(string? modelName)
    {
        var act = () => StrategyReview.Create(Guid.NewGuid(), 1, "review", modelName!);

        act.Should().Throw<ArgumentException>();
    }
}