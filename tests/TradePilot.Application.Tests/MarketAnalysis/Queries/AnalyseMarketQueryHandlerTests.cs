using MediatR;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Indicators;

namespace TradePilot.Application.Tests.MarketAnalysis.Queries;

[TestClass]
public sealed class AnalyseMarketQueryHandlerTests
{
    private const long FourHoursMilliseconds = 14_400_000L;

    [TestMethod]
    public async Task GivenBullishFixture_WhenAnalysing_ThenMapsCompletedCandleFacts()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateTrendFixture(asOf, 220, 100m, 1m);
        var partialCandle = CreateCandle(asOf.ToUnixTimeMilliseconds(), 9_999m);
        candles.Insert(0, partialCandle);
        var sender = CreateSender(candles);
        using var cancellationSource = new CancellationTokenSource();
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var result = await sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", Exchange.Hyperliquid, asOf),
            cancellationSource.Token);

        var completed = candles
            .Where(candle => candle.Timestamp + FourHoursMilliseconds <= asOf.ToUnixTimeMilliseconds())
            .OrderBy(candle => candle.Timestamp)
            .ToList();
        var closes = completed.Select(candle => candle.Close).ToList();
        var bars = completed.Select(candle => (candle.High, candle.Low, candle.Close)).ToList();

        result.Symbol.Should().Be("BTC");
        result.Timeframe.Should().Be("4h");
        result.Timestamp.Should().Be(asOf);
        result.Price.Should().Be(completed[^1].Close);
        result.Indicators.Ema20.Should().Be(EmaCalculator.Calculate(closes, 20));
        result.Indicators.Ema50.Should().Be(EmaCalculator.Calculate(closes, 50));
        result.Indicators.Ema200.Should().Be(EmaCalculator.Calculate(closes, 200));
        result.Indicators.Rsi.Should().Be(RsiCalculator.Calculate(closes, 14));
        result.Indicators.Atr.Should().Be(AtrCalculator.Calculate(bars, 14));
        result.Indicators.AtrPercent.Should().Be(result.Indicators.Atr / result.Price * 100m);
        result.Indicators.DistanceFromEma50Percent.Should().Be(
            (result.Price - result.Indicators.Ema50) / result.Indicators.Ema50 * 100m);
        result.Trend.Should().Be(MarketTrend.Bullish);
        result.Momentum.Should().Be(MarketMomentum.Bullish);
        result.MarketStructure.Should().Be(MarketStructure.Unknown);
        result.RecentSwingHigh.Should().BeNull();
        result.RecentSwingLow.Should().BeNull();
        sender.Verify(candidate => candidate.Send(
            It.Is<GetCandlesQuery>(query =>
                query.Asset == "BTC"
                && query.Timeframe == "4h"
                && query.EndTime == asOf.ToUnixTimeMilliseconds()
                && query.Limit == AnalyseMarketQueryHandler.RequestedCandleCount
                && !query.IncludeIndicators),
            cancellationSource.Token), Times.Once);
    }

    [TestMethod]
    public async Task GivenFlatFixture_WhenAnalysing_ThenReturnsNeutralLowVolatilityFacts()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateTrendFixture(asOf, 200, 100m, 0m, 0.25m);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var result = await sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
            CancellationToken.None);

        result.Trend.Should().Be(MarketTrend.Neutral);
        result.Momentum.Should().Be(MarketMomentum.Neutral);
        result.VolatilityRegime.Should().Be(VolatilityRegime.Low);
        result.Indicators.Rsi.Should().Be(50m);
        result.Indicators.Atr.Should().Be(0.5m);
        result.MarketStructure.Should().Be(MarketStructure.Unknown);
    }

    [TestMethod]
    public async Task GivenBearishFixture_WhenAnalysing_ThenReturnsBearishTrendAndMomentum()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateTrendFixture(asOf, 220, 400m, -1m);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var result = await sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
            CancellationToken.None);

        result.Trend.Should().Be(MarketTrend.Bearish);
        result.Momentum.Should().Be(MarketMomentum.Bearish);
    }

    [TestMethod]
    public async Task GivenConfirmedStructureFixture_WhenAnalysing_ThenExposesRecentSwings()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateMarketStructureFixture(asOf);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var result = await sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
            CancellationToken.None);

        result.MarketStructure.Should().Be(MarketStructure.HigherHighHigherLow);
        result.RecentSwingHigh.Should().Be(12m);
        result.RecentSwingLow.Should().Be(2m);
    }

    [TestMethod]
    public async Task GivenDuplicateTimestamps_WhenAnalysing_ThenDoesNotCountDuplicatesAsHistory()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateTrendFixture(asOf, 199, 100m, 1m);
        candles.Add(candles[100]);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Insufficient completed candle history*199 were available.");
    }

    [TestMethod]
    public async Task GivenEmptyOrShortHistory_WhenAnalysing_ThenReturnsExplicitInsufficientHistoryError()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);

        foreach (var candles in new[]
        {
            new List<CandleDto>(),
            CreateTrendFixture(asOf, 199, 100m, 1m),
        })
        {
            var sender = CreateSender(candles);
            var sut = new AnalyseMarketQueryHandler(sender.Object);

            var action = () => sut.Handle(
                new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
                CancellationToken.None);

            await action.Should().ThrowAsync<DomainException>()
                .WithMessage("Insufficient completed candle history*");
        }
    }

    [TestMethod]
    public async Task GivenZeroLatestCompletedClose_WhenAnalysing_ThenRejectsInvalidAnalysisPrice()
    {
        var asOf = DateTimeOffset.FromUnixTimeMilliseconds(4_000_000_000_000L);
        var candles = CreateTrendFixture(asOf, 200, 100m, 1m);
        candles[^1] = CreateCandle(candles[^1].Timestamp, 0m);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketQuery("BTC", "4h", AsOf: asOf),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("The latest completed candle close must be greater than zero.");
    }

    [TestMethod]
    public async Task GivenStaleCompletedCandles_WhenAnalysing_ThenRejectsStaleMarketData()
    {
        var staleAsOf = DateTimeOffset.UtcNow.AddDays(-10);
        var candles = CreateTrendFixture(staleAsOf, 200, 100m, 1m);
        var sender = CreateSender(candles);
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketQuery("BTC", "4h"),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("The latest completed 4h candle for BTC closed at *, which is stale for analysis as of *.");
    }

    [TestMethod]
    public async Task GivenCancellation_WhenAnalysing_ThenDoesNotRequestCandles()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var sender = new Mock<ISender>();
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketQuery("BTC", "4h"),
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        sender.Verify(candidate => candidate.Send(
            It.IsAny<GetCandlesQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenAnalysing_ThenRejectsBeforeRequestingCandles()
    {
        var sender = new Mock<ISender>();
        var sut = new AnalyseMarketQueryHandler(sender.Object);

        var action = () => sut.Handle(
            new AnalyseMarketQuery("BTC", "6h"),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Invalid timeframe '6h'.*");
        sender.Verify(candidate => candidate.Send(
            It.IsAny<GetCandlesQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ISender> CreateSender(IReadOnlyList<CandleDto> candles)
    {
        var sender = new Mock<ISender>();
        sender.Setup(candidate => candidate.Send(
                It.IsAny<GetCandlesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles.ToList());
        return sender;
    }

    private static List<CandleDto> CreateTrendFixture(
        DateTimeOffset asOf,
        int count,
        decimal initialClose,
        decimal changePerCandle,
        decimal candleRange = 1m)
    {
        var firstOpenTime = asOf.ToUnixTimeMilliseconds() - (count * FourHoursMilliseconds);
        return Enumerable.Range(0, count)
            .Select(index => CreateCandle(
                firstOpenTime + (index * FourHoursMilliseconds),
                initialClose + (index * changePerCandle),
                candleRange))
            .ToList();
    }

    private static List<CandleDto> CreateMarketStructureFixture(DateTimeOffset asOf)
    {
        const int warmupCount = 200;
        var highs = new[] { 6m, 8m, 10m, 8m, 6m, 7m, 6m, 8m, 12m, 8m, 6m, 7m, 6m, 8m };
        var lows = new[] { 6m, 5m, 6m, 5m, 4m, 1m, 4m, 5m, 6m, 5m, 4m, 2m, 4m, 5m };
        var totalCount = warmupCount + highs.Length;
        var firstOpenTime = asOf.ToUnixTimeMilliseconds() - (totalCount * FourHoursMilliseconds);
        var candles = Enumerable.Range(0, warmupCount)
            .Select(index => new CandleDto
            {
                Timestamp = firstOpenTime + (index * FourHoursMilliseconds),
                Open = 50m,
                High = 51m,
                Low = 49m,
                Close = 50m,
            })
            .ToList();

        candles.AddRange(highs.Select((high, index) => new CandleDto
        {
            Timestamp = firstOpenTime + ((warmupCount + index) * FourHoursMilliseconds),
            Open = (high + lows[index]) / 2m,
            High = high,
            Low = lows[index],
            Close = (high + lows[index]) / 2m,
        }));

        return candles;
    }

    private static CandleDto CreateCandle(long timestamp, decimal close, decimal candleRange = 1m)
    {
        return new CandleDto
        {
            Timestamp = timestamp,
            Open = close,
            High = close + candleRange,
            Low = Math.Max(0m, close - candleRange),
            Close = close,
            Volume = 10m,
        };
    }
}
