using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.Application.StrategyEvaluations.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence.Repositories;

namespace TradePilot.Persistence.Tests.Repositories;

[TestClass]
public sealed class StrategyEvaluationRepositoryTests
{
    private static readonly Guid StrategyId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private DbContextOptions<TradePilotDbContext> _contextOptions = null!;

    [TestInitialize]
    public void Setup()
    {
        _contextOptions = new DbContextOptionsBuilder<TradePilotDbContext>()
            .UseInMemoryDatabase($"StrategyEvaluationTests-{Guid.NewGuid():N}")
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [TestMethod]
    public async Task GivenEvaluations_WhenQueried_ThenStrategySymbolDateOrderingAndLimitAreApplied()
    {
        await AddAsync(CreateEvaluation(1_000, "BTC", 4, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));
        await AddAsync(CreateEvaluation(2_000, "BTC", 4, StrategyDecision.EnterLong, Rule("entry.rsi.max", true)));
        await AddAsync(CreateEvaluation(3_000, "ETH", 4, StrategyDecision.NoTrade, Rule("entry.pullback.minimum", false)));
        await AddAsync(CreateEvaluation(4_000, "BTC", 5, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));

        await using var context = CreateContext();
        var sut = CreateRepository(context);
        var result = await sut.GetAsync(
            new StrategyEvaluationFilter(
                StrategyId,
                StrategyVersion: 4,
                Symbol: "btc",
                FromUtc: 900,
                ToUtc: 2_100),
            limit: 1);
        var latest = await sut.GetLatestAsync(
            new StrategyEvaluationFilter(StrategyId, StrategyVersion: 4, Symbol: "BTC"));

        result.Should().ContainSingle();
        result[0].EvaluatedAtUtc.Should().Be(2_000);
        result[0].Rules.Should().ContainSingle();
        latest!.EvaluatedAtUtc.Should().Be(2_000);
        latest.StrategyVersion.Should().Be(4);
    }

    [TestMethod]
    public async Task GivenBlockingFailures_WhenSummaryRequested_ThenEveryActuallyEvaluatedFailedRuleIsCounted()
    {
        await AddAsync(CreateEvaluation(1_000, "BTC", 4, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));
        await AddAsync(CreateEvaluation(2_000, "BTC", 4, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));
        await AddAsync(CreateEvaluation(3_000, "BTC", 4, StrategyDecision.NoTrade, Rule("entry.pullback.minimum", false)));
        await AddAsync(CreateEvaluation(
            4_000,
            "BTC",
            4,
            StrategyDecision.NoTrade,
            Rule("entry.rsi.max", false, 0),
            Rule("volatility.atr.minimum", false, 1)));
        await AddAsync(CreateEvaluation(5_000, "BTC", 4, StrategyDecision.EnterLong, Rule("entry.rsi.max", true)));
        await AddAsync(CreateEvaluation(6_000, "BTC", 4, StrategyDecision.RejectedByRisk, Rule("risk.portfolio_heat", false)));

        await using var context = CreateContext();
        var summary = await CreateRepository(context).GetSummaryAsync(
            new StrategyEvaluationFilter(StrategyId, Symbol: "BTC", FromUtc: 1_000, ToUtc: 6_000));

        summary.TotalEvaluations.Should().Be(6);
        summary.TradeDecisions.Should().Be(1);
        summary.NoTradeDecisions.Should().Be(4);
        summary.RiskRejectedDecisions.Should().Be(1);
        summary.RuleFailureCounts.Should().ContainEquivalentOf(new RuleFailureCount("entry.rsi.max", "entry.rsi.max", 3));
        summary.RuleFailureCounts.Should().ContainEquivalentOf(new RuleFailureCount("entry.pullback.minimum", "entry.pullback.minimum", 1));
        summary.RuleFailureCounts.Should().ContainEquivalentOf(new RuleFailureCount("volatility.atr.minimum", "volatility.atr.minimum", 1));
        summary.MostCommonBlockingRule!.RuleId.Should().Be("entry.rsi.max");
        summary.MostCommonBlockingRule.Count.Should().Be(3);
    }

    [TestMethod]
    public async Task GivenTwoVersions_WhenRetrieved_ThenConfigurationAndVersionIdentityRemainDistinct()
    {
        await AddAsync(CreateEvaluation(1_000, "BTC", 4, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));
        await AddAsync(CreateEvaluation(2_000, "BTC", 5, StrategyDecision.NoTrade, Rule("entry.rsi.max", false)));

        await using var context = CreateContext();
        var results = await CreateRepository(context).GetAsync(
            new StrategyEvaluationFilter(StrategyId, Symbol: "BTC"),
            10);

        results.Select(evaluation => evaluation.StrategyVersion).Should().Equal(5, 4);
        results.Select(evaluation => evaluation.ConfigurationIdentity).Should().OnlyHaveUniqueItems();
    }

    private async Task AddAsync(StrategyEvaluation evaluation)
    {
        await using var context = CreateContext();
        await CreateRepository(context).AddAsync(evaluation);
    }

    private TradePilotDbContext CreateContext() => new(_contextOptions);

    private static StrategyEvaluationRepository CreateRepository(TradePilotDbContext context)
    {
        return new StrategyEvaluationRepository(context, NullLogger<StrategyEvaluationRepository>.Instance);
    }

    private static StrategyEvaluation CreateEvaluation(
        long timestamp,
        string symbol,
        int version,
        StrategyDecision decision,
        params RuleEvaluation[] rules)
    {
        return StrategyEvaluation.Create(
            StrategyId,
            "v10.4",
            version,
            new string((char)('a' + version), 64),
            symbol,
            "15m",
            timestamp,
            decision,
            decision is not StrategyDecision.NoTrade,
            rules.FirstOrDefault(rule => !rule.Passed)?.Reason,
            timestamp,
            60_000m,
            "Normal",
            decision == StrategyDecision.EnterLong ? "OpenPosition" : null,
            null,
            false,
            rules);
    }

    private static RuleEvaluation Rule(string ruleId, bool passed, int order = 0)
    {
        return RuleEvaluation.Create(
            order,
            ruleId,
            ruleId,
            ruleId.StartsWith("risk.", StringComparison.Ordinal) ? RuleCategory.Risk : RuleCategory.Entry,
            passed,
            passed ? $"{ruleId} passed." : $"{ruleId} failed.",
            !passed,
            ruleId.StartsWith("risk.", StringComparison.Ordinal)
                ? RuleEvaluationKind.RiskOverride
                : RuleEvaluationKind.Blocking);
    }
}
