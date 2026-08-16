using MediatR;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;

namespace TradePilot.Application.Tests.MarketAnalysis.Queries;

[TestClass]
public sealed class AnalyseMarketMultiTimeframeQueryHandlerTests
{
    private static readonly string[] DefaultFixtureTimeframes = ["15m", "1h", "4h", "1d"];

    [TestMethod]
    public async Task GivenFullyBullishTrends_WhenAnalysing_ThenReturnsAlignedBullishFacts()
    {
        var results = CreateResults(MarketTrend.Bullish);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTrend.Should().Be(MarketTrend.Bullish);
        result.ShortTermTrend.Should().Be(MarketTrend.Bullish);
        result.BullishTrendCount.Should().Be(4);
        result.BearishTrendCount.Should().Be(0);
        result.NeutralTrendCount.Should().Be(0);
        result.TrendAlignment.Should().Be(DirectionalAlignment.AlignedBullish);
        result.Conflicts.HasTrendConflict.Should().BeFalse();
        result.Conflicts.Trends.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenFullyBearishTrends_WhenAnalysing_ThenReturnsAlignedBearishFacts()
    {
        var results = CreateResults(MarketTrend.Bearish);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTrend.Should().Be(MarketTrend.Bearish);
        result.ShortTermTrend.Should().Be(MarketTrend.Bearish);
        result.BearishTrendCount.Should().Be(4);
        result.TrendAlignment.Should().Be(DirectionalAlignment.AlignedBearish);
        result.Conflicts.HasTrendConflict.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenFullyNeutralTrends_WhenAnalysing_ThenReturnsAlignedNeutralFacts()
    {
        var results = CreateResults(MarketTrend.Neutral);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTrend.Should().Be(MarketTrend.Neutral);
        result.ShortTermTrend.Should().Be(MarketTrend.Neutral);
        result.NeutralTrendCount.Should().Be(4);
        result.TrendAlignment.Should().Be(DirectionalAlignment.AlignedNeutral);
        result.Conflicts.HasTrendConflict.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenMostlyBullishTrends_WhenAnalysing_ThenUsesLongestAndShortestTimeframes()
    {
        var results = CreateResults(MarketTrend.Bullish);
        results["15m"] = CreateAnalysis("15m", MarketTrend.Neutral);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTimeframe.Should().Be("1d");
        result.PrimaryTrend.Should().Be(MarketTrend.Bullish);
        result.ShortTermTimeframe.Should().Be("15m");
        result.ShortTermTrend.Should().Be(MarketTrend.Neutral);
        result.TrendAlignment.Should().Be(DirectionalAlignment.MostlyBullish);
        result.BullishTrendCount.Should().Be(3);
        result.NeutralTrendCount.Should().Be(1);
        result.Conflicts.PrimaryVsShortTermTrendConflict.Should().BeTrue();
        result.Conflicts.Trends.Should().ContainSingle().Which.Should().Be(
            new TimeframeClassificationConflict<MarketTrend>(
                "15m",
                MarketTrend.Neutral,
                "1d",
                MarketTrend.Bullish));
    }

    [TestMethod]
    public async Task GivenMostlyBearishTrends_WhenAnalysing_ThenReturnsMostlyBearishFacts()
    {
        var results = CreateResults(MarketTrend.Bearish);
        results["15m"] = CreateAnalysis("15m", MarketTrend.Neutral);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTrend.Should().Be(MarketTrend.Bearish);
        result.ShortTermTrend.Should().Be(MarketTrend.Neutral);
        result.TrendAlignment.Should().Be(DirectionalAlignment.MostlyBearish);
        result.BearishTrendCount.Should().Be(3);
        result.NeutralTrendCount.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenBullishAndBearishTrends_WhenAnalysing_ThenReturnsTypedConflictEvidence()
    {
        var results = CreateResults(MarketTrend.Bullish);
        results["15m"] = CreateAnalysis("15m", MarketTrend.Bearish);
        results["1h"] = CreateAnalysis("1h", MarketTrend.Neutral);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.PrimaryTrend.Should().Be(MarketTrend.Bullish);
        result.ShortTermTrend.Should().Be(MarketTrend.Bearish);
        result.TrendAlignment.Should().Be(DirectionalAlignment.Mixed);
        result.BullishTrendCount.Should().Be(2);
        result.BearishTrendCount.Should().Be(1);
        result.NeutralTrendCount.Should().Be(1);
        result.Conflicts.PrimaryVsShortTermTrendConflict.Should().BeTrue();
        result.Conflicts.BullishAndBearishTrendsPresent.Should().BeTrue();
        result.Conflicts.Trends.Should().Equal(
            new TimeframeClassificationConflict<MarketTrend>(
                "15m",
                MarketTrend.Bearish,
                "1d",
                MarketTrend.Bullish),
            new TimeframeClassificationConflict<MarketTrend>(
                "1h",
                MarketTrend.Neutral,
                "1d",
                MarketTrend.Bullish));
    }

    [TestMethod]
    public async Task GivenConflictingMomentumAndAlignedTrend_WhenAnalysing_ThenKeepsAlignmentsIndependent()
    {
        var results = CreateResults(MarketTrend.Bullish, MarketMomentum.Bullish);
        results["15m"] = CreateAnalysis("15m", MarketTrend.Bullish, MarketMomentum.Bearish);
        results["1h"] = CreateAnalysis("1h", MarketTrend.Bullish, MarketMomentum.Neutral);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.TrendAlignment.Should().Be(DirectionalAlignment.AlignedBullish);
        result.MomentumAlignment.Should().Be(DirectionalAlignment.Mixed);
        result.BullishMomentumCount.Should().Be(2);
        result.BearishMomentumCount.Should().Be(1);
        result.NeutralMomentumCount.Should().Be(1);
        result.Conflicts.Trends.Should().BeEmpty();
        result.Conflicts.Momentum.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GivenLowerTimeframeStructureConflict_WhenAnalysing_ThenPreservesPhaseTwoStructureValues()
    {
        var results = CreateResults(
            MarketTrend.Bullish,
            marketStructure: MarketStructure.HigherHighHigherLow);
        results["15m"] = CreateAnalysis(
            "15m",
            MarketTrend.Bullish,
            marketStructure: MarketStructure.LowerHighLowerLow);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.StructureAlignment.Should().Be(StructureAlignment.Mixed);
        result.HigherHighHigherLowStructureCount.Should().Be(3);
        result.LowerHighLowerLowStructureCount.Should().Be(1);
        result.RangeStructureCount.Should().Be(0);
        result.MixedStructureCount.Should().Be(0);
        result.UnknownStructureCount.Should().Be(0);
        result.Conflicts.MarketStructures.Should().ContainSingle().Which.Value
            .Should().Be(MarketStructure.LowerHighLowerLow);
    }

    [TestMethod]
    public async Task GivenVolatilityDisagreement_WhenAnalysing_ThenReturnsRegimeCountsAndEvidence()
    {
        var results = CreateResults(MarketTrend.Neutral, volatilityRegime: VolatilityRegime.Low);
        results["15m"] = CreateAnalysis("15m", MarketTrend.Neutral, volatilityRegime: VolatilityRegime.High);
        results["1h"] = CreateAnalysis("1h", MarketTrend.Neutral, volatilityRegime: VolatilityRegime.High);
        results["4h"] = CreateAnalysis("4h", MarketTrend.Neutral, volatilityRegime: VolatilityRegime.Normal);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        result.VolatilityAlignment.Should().Be(VolatilityAlignment.Mixed);
        result.HighVolatilityCount.Should().Be(2);
        result.NormalVolatilityCount.Should().Be(1);
        result.LowVolatilityCount.Should().Be(1);
        result.Conflicts.VolatilityRegimes.Select(conflict => conflict.Timeframe)
            .Should().Equal("15m", "1h", "4h");
        result.Timeframes.Select(item => item.Analysis.VolatilityRegime)
            .Should().Equal(
                VolatilityRegime.High,
                VolatilityRegime.High,
                VolatilityRegime.Normal,
                VolatilityRegime.Low);
    }

    [TestMethod]
    public async Task GivenUnusualOrderAndAliases_WhenAnalysing_ThenCanonicalizesOrdersAndDeduplicates()
    {
        var asOf = DateTimeOffset.Parse("2026-08-14T09:00:00Z");
        var results = CreateResults(MarketTrend.Bullish);
        var requests = new List<AnalyseMarketQuery>();
        var sender = CreateSender(results, requests);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery(
                " BTC ",
                ["1D", "4H", "15m", "1h", "1H"],
                AsOf: asOf),
            CancellationToken.None);

        result.Symbol.Should().Be("BTC");
        result.TotalTimeframes.Should().Be(4);
        result.Timeframes.Select(item => item.Timeframe).Should().Equal("15m", "1h", "4h", "1d");
        result.Timeframes.Select(item => item.Analysis).Should().Equal(
            results["15m"],
            results["1h"],
            results["4h"],
            results["1d"]);
        requests.Select(request => request.Timeframe).Should().Equal("15m", "1h", "4h", "1d");
        requests.Should().OnlyContain(request => request.AsOf == asOf && request.Symbol == " BTC ");
        sender.Verify(candidate => candidate.Send(
            It.IsAny<AnalyseMarketQuery>(),
            It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [TestMethod]
    public async Task GivenOneDistinctTimeframe_WhenAnalysing_ThenRejectsBeforePhaseTwoCalls()
    {
        var sender = new Mock<ISender>();
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", ["1H", "1h"]),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("At least two distinct timeframes are required*");
        sender.Verify(candidate => candidate.Send(
            It.IsAny<AnalyseMarketQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenAnalysing_ThenRejectsBeforePhaseTwoCalls()
    {
        var sender = new Mock<ISender>();
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", ["15m", "6h"]),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Invalid timeframe '6h'.*");
        sender.Verify(candidate => candidate.Send(
            It.IsAny<AnalyseMarketQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenUnderlyingAnalysisFailure_WhenAnalysing_ThenFailsWholeRequestWithoutDroppingTimeframe()
    {
        var results = CreateResults(MarketTrend.Bullish);
        var sender = CreateSender(results);
        sender.Setup(candidate => candidate.Send(
                It.Is<AnalyseMarketQuery>(query => query.Timeframe == "4h"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Insufficient completed candle history for BTC/4h."));
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", DefaultFixtureTimeframes),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Insufficient completed candle history for BTC/4h.");
        sender.Verify(candidate => candidate.Send(
            It.Is<AnalyseMarketQuery>(query => query.Timeframe == "1d"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenCancellation_WhenAnalysing_ThenPropagatesTokenToPhaseTwo()
    {
        using var cancellationSource = new CancellationTokenSource();
        var observedToken = CancellationToken.None;
        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.IsAny<AnalyseMarketQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns((AnalyseMarketQuery _, CancellationToken token) =>
            {
                observedToken = token;
                cancellationSource.Cancel();
                return Task.FromCanceled<MarketAnalysisResult>(token);
            });
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", ["15m", "1h"]),
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        observedToken.Should().Be(cancellationSource.Token);
        sender.Verify(candidate => candidate.Send(
            It.IsAny<AnalyseMarketQuery>(),
            cancellationSource.Token), Times.Once);
    }

    [TestMethod]
    public async Task GivenNoStrictDirectionalMajority_WhenAnalysing_ThenReturnsMixed()
    {
        var results = new Dictionary<string, MarketAnalysisResult>
        {
            ["15m"] = CreateAnalysis("15m", MarketTrend.Bullish),
            ["1h"] = CreateAnalysis("1h", MarketTrend.Neutral),
        };
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(CreateSender(results).Object);

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", ["15m", "1h"]),
            CancellationToken.None);

        result.TrendAlignment.Should().Be(DirectionalAlignment.Mixed);
    }

    [TestMethod]
    public async Task GivenNoAsOf_WhenAnalysing_ThenUsesOneSharedCutoffAndRecordsGenerationTime()
    {
        var now = DateTimeOffset.Parse("2026-08-14T10:15:00Z");
        var results = CreateResults(MarketTrend.Bullish);
        var requests = new List<AnalyseMarketQuery>();
        var sender = CreateSender(results, requests);
        var sut = new AnalyseMarketMultiTimeframeQueryHandler(sender.Object, new FixedTimeProvider(now));

        var result = await sut.Handle(
            new AnalyseMarketMultiTimeframeQuery("BTC", ["15m", "1h"]),
            CancellationToken.None);

        result.GeneratedAt.Should().Be(now);
        requests.Should().OnlyContain(request => request.AsOf == now);
    }

    /// <summary>
    /// Creates one complete representative Phase 2 result per standard fixture timeframe.
    /// </summary>
    private static Dictionary<string, MarketAnalysisResult> CreateResults(
        MarketTrend trend,
        MarketMomentum momentum = MarketMomentum.Neutral,
        VolatilityRegime volatilityRegime = VolatilityRegime.Normal,
        MarketStructure marketStructure = MarketStructure.Unknown)
    {
        return DefaultFixtureTimeframes.ToDictionary(
            timeframe => timeframe,
            timeframe => CreateAnalysis(timeframe, trend, momentum, volatilityRegime, marketStructure));
    }

    /// <summary>
    /// Creates a strongly typed Phase 2 fixture without rebuilding indicator calculations.
    /// </summary>
    private static MarketAnalysisResult CreateAnalysis(
        string timeframe,
        MarketTrend trend,
        MarketMomentum momentum = MarketMomentum.Neutral,
        VolatilityRegime volatilityRegime = VolatilityRegime.Normal,
        MarketStructure marketStructure = MarketStructure.Unknown)
    {
        return new MarketAnalysisResult(
            "BTC",
            timeframe,
            DateTimeOffset.Parse("2026-08-14T08:00:00Z").AddMinutes(timeframe.Length),
            100m + timeframe.Length,
            new MarketIndicatorValues(
                101m,
                99m,
                95m,
                52m,
                2m,
                2m,
                1m,
                3m,
                7m),
            trend,
            momentum,
            volatilityRegime,
            marketStructure,
            110m,
            90m);
    }

    /// <summary>
    /// Creates a mediator fixture that returns the supplied Phase 2 result by canonical timeframe.
    /// </summary>
    private static Mock<ISender> CreateSender(
        IReadOnlyDictionary<string, MarketAnalysisResult> results,
        ICollection<AnalyseMarketQuery>? requests = null)
    {
        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.IsAny<AnalyseMarketQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns((AnalyseMarketQuery query, CancellationToken _) =>
            {
                requests?.Add(query);
                return Task.FromResult(results[query.Timeframe]);
            });
        return sender;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
