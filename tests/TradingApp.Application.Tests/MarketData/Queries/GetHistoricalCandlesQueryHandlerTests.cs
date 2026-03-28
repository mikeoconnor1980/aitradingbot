using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.MarketData.Queries;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.MarketData.Queries;

[TestClass]
public sealed class GetHistoricalCandlesQueryHandlerTests
{
    private readonly Mock<ICandleRepository> _candleRepositoryMock = new();

    [TestMethod]
    public async Task GivenRepositoryCandles_WhenHandle_ThenReturnsMappedDtos()
    {
        var candles = new List<Candle>
        {
            Candle.Create("BTC", "15m", 1_700_000_000_000, 100m, 110m, 90m, 105m, 50m, 10),
            Candle.Create("BTC", "15m", 1_700_000_900_000, 105m, 115m, 95m, 110m, 60m, 11),
        };

        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var result = await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "15m"), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(candles[0].Timestamp);
        result[0].Open.Should().Be(100m);
        result[0].High.Should().Be(110m);
        result[0].Low.Should().Be(90m);
        result[0].Close.Should().Be(105m);
        result[0].Volume.Should().Be(50m);
    }

    [TestMethod]
    public async Task GivenPerpAsset_WhenHandle_ThenStripsPerpSuffixBeforeCallingRepository()
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Candle>());

        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "15m"), CancellationToken.None);

        _candleRepositoryMock.Verify(
            repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMoreThanDefaultLimit_WhenHandle_ThenReturnsLastFiveHundredCandles()
    {
        var candles = Enumerable.Range(0, 600)
            .Select(index => Candle.Create(
                "BTC",
                "15m",
                1_700_000_000_000 + (index * 900_000L),
                100m,
                110m,
                90m,
                105m,
                50m,
                10))
            .ToList();

        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);

        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var result = await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "15m"), CancellationToken.None);

        result.Should().HaveCount(500);
        result[0].Timestamp.Should().Be(candles[100].Timestamp);
        result[^1].Timestamp.Should().Be(candles[^1].Timestamp);
    }

    [TestMethod]
    public async Task GivenExplicitEndTimeAndLimit_WhenHandle_ThenCalculatesExpectedTimeRange()
    {
        const long endTime = 1_700_000_000_000;
        const int limit = 100;
        const long expectedStartTime = endTime - (limit * 900_000L);

        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                expectedStartTime,
                endTime,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Candle>());

        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "15m", endTime, limit), CancellationToken.None);

        _candleRepositoryMock.Verify(
            repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                expectedStartTime,
                endTime,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenRepositoryReturnsNoCandles_WhenHandle_ThenReturnsEmptyList()
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCandlesAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Candle>());

        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var result = await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "15m"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task GivenEmptyAsset_WhenHandle_ThenThrowsDomainException(string? asset)
    {
        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var action = async () => await sut.Handle(new GetHistoricalCandlesQuery(asset!, "15m"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>();
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public async Task GivenEmptyTimeframe_WhenHandle_ThenThrowsDomainException(string? timeframe)
    {
        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var action = async () => await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", timeframe!), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>();
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenHandle_ThenThrowsDomainException()
    {
        var sut = new GetHistoricalCandlesQueryHandler(_candleRepositoryMock.Object);

        var action = async () => await sut.Handle(new GetHistoricalCandlesQuery("BTC-PERP", "2m"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Invalid timeframe '2m'.*");
    }
}