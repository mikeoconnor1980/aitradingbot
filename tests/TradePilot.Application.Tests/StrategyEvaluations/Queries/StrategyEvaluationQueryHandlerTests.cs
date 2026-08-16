using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.StrategyEvaluations.Models;
using TradePilot.Application.StrategyEvaluations.Queries;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Tests.StrategyEvaluations.Queries;

[TestClass]
public sealed class StrategyEvaluationQueryHandlerTests
{
    [TestMethod]
    public async Task GivenBoundedHistoryRequest_WhenHandled_ThenFiltersAndMaximumLimitAreDelegated()
    {
        var repository = new Mock<IStrategyEvaluationRepository>();
        repository
            .Setup(value => value.GetAsync(
                It.Is<StrategyEvaluationFilter>(filter =>
                    filter.StrategyName == "v10.4"
                    && filter.Symbol == "BTC"
                    && filter.FromUtc == 1_000
                    && filter.ToUtc == 2_000),
                GetStrategyEvaluationsQueryHandler.MaximumLimit,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEvaluation()]);
        var sut = new GetStrategyEvaluationsQueryHandler(repository.Object);

        var result = await sut.Handle(
            new GetStrategyEvaluationsQuery(
                StrategyName: "v10.4",
                Symbol: "BTC",
                From: DateTimeOffset.FromUnixTimeMilliseconds(1_000),
                To: DateTimeOffset.FromUnixTimeMilliseconds(2_000),
                Limit: 1_000),
            CancellationToken.None);

        result.Limit.Should().Be(GetStrategyEvaluationsQueryHandler.MaximumLimit);
        result.Evaluations.Should().ContainSingle();
    }

    [TestMethod]
    public async Task GivenLatestRequest_WhenHandled_ThenHistoricalCutoffIsDelegatedWithoutCurrentMarketLookup()
    {
        var evaluation = CreateEvaluation();
        var repository = new Mock<IStrategyEvaluationRepository>();
        repository
            .Setup(value => value.GetLatestAsync(
                It.Is<StrategyEvaluationFilter>(filter =>
                    filter.StrategyId == evaluation.StrategyId
                    && filter.Symbol == "BTC"
                    && filter.ToUtc == 2_000),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluation);
        var sut = new GetLatestStrategyEvaluationQueryHandler(repository.Object);

        var result = await sut.Handle(
            new GetLatestStrategyEvaluationQuery(
                StrategyId: evaluation.StrategyId,
                Symbol: "BTC",
                AtOrBefore: DateTimeOffset.FromUnixTimeMilliseconds(2_000)),
            CancellationToken.None);

        result.Should().BeSameAs(evaluation);
    }

    [TestMethod]
    public async Task GivenSummaryRequest_WhenHandled_ThenDatabaseAggregationResultIsReturnedUnchanged()
    {
        var expected = new StrategyEvaluationSummary(
            4,
            1,
            1,
            3,
            0,
            [new RuleFailureCount("entry.rsi.max", "Maximum RSI", 3)],
            new RuleFailureCount("entry.rsi.max", "Maximum RSI", 3));
        var repository = new Mock<IStrategyEvaluationRepository>();
        repository
            .Setup(value => value.GetSummaryAsync(It.IsAny<StrategyEvaluationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var sut = new GetStrategyEvaluationSummaryQueryHandler(repository.Object);

        var result = await sut.Handle(
            new GetStrategyEvaluationSummaryQuery(StrategyName: "v10.4", Symbol: "BTC"),
            CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [TestMethod]
    public async Task GivenNoStrategyIdentity_WhenHandled_ThenUnboundedCrossStrategyQueryIsRejected()
    {
        var sut = new GetStrategyEvaluationsQueryHandler(Mock.Of<IStrategyEvaluationRepository>());

        var action = () => sut.Handle(new GetStrategyEvaluationsQuery(Symbol: "BTC"), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    private static StrategyEvaluation CreateEvaluation()
    {
        return StrategyEvaluation.Create(
            Guid.NewGuid(),
            "v10.4",
            4,
            new string('a', 64),
            "BTC",
            "15m",
            1_000,
            StrategyDecision.NoTrade,
            false,
            "RSI failed.",
            1_000,
            60_000m,
            "Normal",
            null,
            null,
            false,
            [RuleEvaluation.Create(0, "entry.rsi.max", "Maximum RSI", RuleCategory.Momentum, false, "RSI failed.", true, RuleEvaluationKind.Blocking)]);
    }
}
