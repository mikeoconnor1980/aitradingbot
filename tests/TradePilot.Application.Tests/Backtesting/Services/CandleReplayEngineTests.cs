using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Backtesting.Models;
using TradePilot.Application.Backtesting.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class CandleReplayEngineTests
{
    private const long FifteenMinutesMs = 15L * 60L * 1000L;
    private const long OneHourMs = 60L * 60L * 1000L;
    private const long FourHoursMs = 4L * 60L * 60L * 1000L;

    private Mock<ICandleRepository> _candleRepositoryMock = default!;
    private CandleReplayEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _candleRepositoryMock = new Mock<ICandleRepository>();
        _sut = new CandleReplayEngine(_candleRepositoryMock.Object);
    }

    [TestMethod]
    public async Task GivenAllTimeframesAvailable_WhenLoadAsync_ThenReturnsSortedReplayDataAndWarmupIndex()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2);

        SetupCandles("15m", CreateCandles("15m", 12 * OneHourMs, 11 * OneHourMs + 45 * 60L * 1000L, 11 * OneHourMs + 30 * 60L * 1000L, 12 * OneHourMs + FifteenMinutesMs));
        SetupCandles("1h", CreateCandles("1h", 12 * OneHourMs, 11 * OneHourMs, 10 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 8 * OneHourMs, 4 * OneHourMs));

        var replayData = await _sut.LoadAsync(config);

        replayData.Candles15m.Select(candle => candle.Timestamp).Should().Equal(
            11 * OneHourMs + 30 * 60L * 1000L,
            11 * OneHourMs + 45 * 60L * 1000L,
            12 * OneHourMs,
            12 * OneHourMs + FifteenMinutesMs);
        replayData.WarmupEndIndex.Should().Be(2);

        _candleRepositoryMock.Verify(repository => repository.GetCandlesAsync(
            config.Symbol,
            "15m",
            11 * OneHourMs + 30 * 60L * 1000L,
            config.EndDateUtc,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _candleRepositoryMock.Verify(repository => repository.GetCandlesAsync(
            config.Symbol,
            "1h",
            10 * OneHourMs,
            config.EndDateUtc,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _candleRepositoryMock.Verify(repository => repository.GetCandlesAsync(
            config.Symbol,
            "4h",
            4 * OneHourMs,
            config.EndDateUtc,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void GivenHigherTimeframeCandles_WhenGetLatestClosedCandle_ThenReturnsLatestClosedWithoutLookahead()
    {
        var oneHourCandles = CreateCandles("1h", 10 * OneHourMs, 11 * OneHourMs, 12 * OneHourMs);

        var latestClosed = CandleReplayEngine.GetLatestClosedCandle(oneHourCandles, 12 * OneHourMs);

        latestClosed.Should().NotBeNull();
        latestClosed!.Timestamp.Should().Be(11 * OneHourMs);
    }

    [TestMethod]
    public async Task GivenNoEvaluationCandles_WhenLoadAsync_ThenThrowsDescriptiveError()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2);

        SetupCandles("15m", CreateCandles("15m", 11 * OneHourMs + 30 * 60L * 1000L, 11 * OneHourMs + 45 * 60L * 1000L));
        SetupCandles("1h", CreateCandles("1h", 10 * OneHourMs, 11 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 4 * OneHourMs, 8 * OneHourMs));

        var action = () => _sut.LoadAsync(config);

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No candle data found for BTC/15m*");
    }

    [TestMethod]
    public async Task GivenNoClosedOneHourCandleAtEvaluationStart_WhenLoadAsync_ThenThrowsDescriptiveError()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2);

        SetupCandles("15m", CreateCandles("15m", 11 * OneHourMs + 30 * 60L * 1000L, 11 * OneHourMs + 45 * 60L * 1000L, 12 * OneHourMs));
        SetupCandles("1h", CreateCandles("1h", 12 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 4 * OneHourMs, 8 * OneHourMs));

        var action = () => _sut.LoadAsync(config);

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Missing 1h candle data for BTC*");
    }

    [TestMethod]
    public async Task GivenMissingFourHourCandles_WhenLoadAsync_ThenThrowsDescriptiveError()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2);

        SetupCandles("15m", CreateCandles("15m", 11 * OneHourMs + 30 * 60L * 1000L, 11 * OneHourMs + 45 * 60L * 1000L, 12 * OneHourMs));
        SetupCandles("1h", CreateCandles("1h", 10 * OneHourMs, 11 * OneHourMs));
        SetupCandles("4h", []);

        var action = () => _sut.LoadAsync(config);

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Missing 4h candle data for BTC*");
    }

    [TestMethod]
    public async Task GivenInsufficientWarmupCandles_WhenLoadAsync_ThenThrowsDescriptiveError()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 3);

        SetupCandles("15m", CreateCandles("15m", 11 * OneHourMs + 30 * 60L * 1000L, 11 * OneHourMs + 45 * 60L * 1000L, 12 * OneHourMs));
        SetupCandles("1h", CreateCandles("1h", 10 * OneHourMs, 11 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 4 * OneHourMs, 8 * OneHourMs));

        var action = () => _sut.LoadAsync(config);

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Insufficient warmup data for BTC/15m*");
    }

    [TestMethod]
    public async Task GivenFourHourTriggerTimeframe_WhenLoadAsync_ThenUsesFourHourCandlesAsTriggerSeries()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 24 * OneHourMs, warmupPeriod: 2, triggerTimeframe: "4h");

        SetupCandles("15m", CreateCandles("15m", 4 * OneHourMs, 8 * OneHourMs, 12 * OneHourMs, 16 * OneHourMs));
        SetupCandles("1h", CreateCandles("1h", 4 * OneHourMs, 8 * OneHourMs, 12 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 1 * OneHourMs, 4 * OneHourMs, 8 * OneHourMs, 12 * OneHourMs, 16 * OneHourMs));

        var replayData = await _sut.LoadAsync(config);

        replayData.TriggerCandles.Should().BeSameAs(replayData.Candles4h);
        replayData.TriggerTimeframe.Should().Be("4h");
        replayData.WarmupEndIndex.Should().Be(3);
    }

    [TestMethod]
    public async Task GivenOneHourTriggerTimeframe_WhenLoadAsync_ThenUsesOneHourCandlesAsTriggerSeries()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2, triggerTimeframe: "1h");

        SetupCandles("15m", CreateCandles("15m", 10 * OneHourMs, 11 * OneHourMs, 12 * OneHourMs));
        SetupCandles("1h", CreateCandles("1h", 8 * OneHourMs, 10 * OneHourMs, 11 * OneHourMs, 12 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 4 * OneHourMs, 8 * OneHourMs));

        var replayData = await _sut.LoadAsync(config);

        replayData.TriggerCandles.Should().BeSameAs(replayData.Candles1h);
        replayData.TriggerTimeframe.Should().Be("1h");
    }

    [TestMethod]
    public async Task GivenDefaultTriggerTimeframe_WhenLoadAsync_ThenUsesFifteenMinCandlesAsTriggerSeries()
    {
        var config = CreateConfig(startDateUtc: 12 * OneHourMs, endDateUtc: 13 * OneHourMs, warmupPeriod: 2);

        SetupCandles("15m", CreateCandles("15m", 11 * OneHourMs + 30 * 60L * 1000L, 11 * OneHourMs + 45 * 60L * 1000L, 12 * OneHourMs, 12 * OneHourMs + FifteenMinutesMs));
        SetupCandles("1h", CreateCandles("1h", 10 * OneHourMs, 11 * OneHourMs));
        SetupCandles("4h", CreateCandles("4h", 4 * OneHourMs, 8 * OneHourMs));

        var replayData = await _sut.LoadAsync(config);

        replayData.TriggerCandles.Should().BeSameAs(replayData.Candles15m);
        replayData.TriggerTimeframe.Should().Be("15m");
    }

    private void SetupCandles(string interval, IReadOnlyList<Candle> candles)
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                It.IsAny<string>(),
                interval,
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);
    }

    private static BacktestConfig CreateConfig(long startDateUtc, long endDateUtc, int warmupPeriod, string triggerTimeframe = "15m")
    {
        return new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            InitialCapital = 10_000m,
            TriggerTimeframe = triggerTimeframe,
            Strategy = new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Grid,
                StrategyName = "Test",
                Market = "BTC-USD",
                Grid = new GridConfig { Levels = 5, Spacing = 0.5m },
                Risk = new RiskConfig { PositionSizeValue = 100m, Leverage = 1m, MaxOpenTrades = 1 },
            },
            Execution = new ExecutionConfig
            {
                FeeModel = FeeModel.Default,
            },
            WarmupPeriod = warmupPeriod,
        };
    }

    private static IReadOnlyList<Candle> CreateCandles(string interval, params long[] timestamps)
    {
        return timestamps
            .Select((timestamp, index) => Candle.Create(
                "Binance",
                "BTC",
                interval,
                timestamp,
                100m + index,
                101m + index,
                99m + index,
                100.5m + index,
                1_000m,
                10))
            .ToList();
    }
}