using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Candles.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Api.Tests.Services;

[TestClass]
public sealed class BinanceCandleIngestionServiceTests
{
    private static readonly DateTime DefaultStartDate = new(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string BinanceSource = "Binance";

    private Mock<IBinanceFuturesRestClient> _restClientMock = default!;
    private Mock<ICandleRepository> _repositoryMock = default!;
    private Mock<ILogger<BinanceCandleIngestionService>> _loggerMock = default!;
    private BinanceCandleIngestionService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IBinanceFuturesRestClient>(MockBehavior.Strict);
        _repositoryMock = new Mock<ICandleRepository>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<BinanceCandleIngestionService>>();
        _sut = CreateSut();
    }

    [TestMethod]
    public async Task GivenExistingBinanceCandles_WhenIngest_ThenResumesFromLatestTimestamp()
    {
        const long latestTimestamp = 1700000000000L;
        const long explicitEndTime = latestTimestamp + 3_600_000L + 1;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", BinanceSource, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestTimestamp);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", latestTimestamp + 1, explicitEndTime, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"], EndTime = explicitEndTime });

        _restClientMock.Verify(
            client => client.GetKlinesAsync("BTCUSDT", "1h", latestTimestamp + 1, explicitEndTime, 1500, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenEmptyDatabase_WhenIngest_ThenFetchesFromDefaultStartDate()
    {
        var expectedStart = new DateTimeOffset(DefaultStartDate).ToUnixTimeMilliseconds();
        var explicitEndTime = expectedStart + 3_600_000L;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", BinanceSource, It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", expectedStart, It.IsAny<long?>(), 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"], EndTime = explicitEndTime });

        result.TotalFetched.Should().Be(0);
        result.Intervals.Should().ContainSingle();
        result.Intervals[0].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenPagedResponses_WhenIngest_ThenSortsPersistsAndTagsBinanceCandles()
    {
        const long requestStartTime = 1700000000000L;
        var intervalMs = 3_600_000L;
        var batches = new Queue<IReadOnlyList<CandleSnapshotDto>>([
            new List<CandleSnapshotDto>
            {
                new() { Timestamp = requestStartTime + intervalMs, Open = 2m, High = 3m, Low = 1m, Close = 2.5m, Volume = 20m, NumTrades = 12 },
                new() { Timestamp = requestStartTime, Open = 1m, High = 2m, Low = 0.5m, Close = 1.5m, Volume = 10m, NumTrades = 10 },
                new() { Timestamp = requestStartTime + (intervalMs * 2), Open = 3m, High = 4m, Low = 2m, Close = 3.5m, Volume = 30m, NumTrades = 14 },
            },
            new List<CandleSnapshotDto>
            {
                new() { Timestamp = requestStartTime + (intervalMs * 3), Open = 4m, High = 5m, Low = 3m, Close = 4.5m, Volume = 40m, NumTrades = 16 },
            },
            Array.Empty<CandleSnapshotDto>(),
        ]);

        var persistedBatches = new List<IReadOnlyList<Candle>>();
        _sut = CreateSut(pageSize: 3);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Candle>, CancellationToken>((candles, _) => persistedBatches.Add(candles.ToList()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", It.IsAny<long>(), It.IsAny<long?>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => batches.Dequeue());

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["1h"],
            StartTime = requestStartTime,
            EndTime = requestStartTime + (intervalMs * 4) + 1,
        });

        result.TotalFetched.Should().Be(4);
        result.TotalInserted.Should().Be(4);
        persistedBatches.Should().HaveCount(2);
        persistedBatches[0].Select(candle => candle.Timestamp).Should().Equal(
            requestStartTime,
            requestStartTime + intervalMs,
            requestStartTime + (intervalMs * 2));
        persistedBatches.SelectMany(batch => batch).Should().OnlyContain(candle => candle.Source == BinanceSource && candle.Symbol == "BTC");
    }

    [TestMethod]
    public async Task GivenConsecutiveEmptyBatches_WhenIngest_ThenUsesBinarySearchToFindNextData()
    {
        const long intervalMs = 3_600_000L;
        const long requestStartTime = 0L;
        var dataStart = intervalMs * 10;
        var endTime = intervalMs * 12;
        var requestStarts = new List<long>();

        _sut = CreateSut(pageSize: 2);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", It.IsAny<long>(), It.IsAny<long?>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, long startTime, long? rangeEndTime, int _, CancellationToken _) =>
            {
                requestStarts.Add(startTime);
                if (rangeEndTime.HasValue && startTime <= dataStart && rangeEndTime.Value >= dataStart)
                {
                    return
                    [
                        new CandleSnapshotDto
                        {
                            Timestamp = dataStart,
                            Open = 1m,
                            High = 2m,
                            Low = 0.5m,
                            Close = 1.5m,
                            Volume = 10m,
                            NumTrades = 5,
                        },
                    ];
                }

                return Array.Empty<CandleSnapshotDto>();
            });

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["1h"],
            StartTime = requestStartTime,
            EndTime = endTime,
        });

        result.TotalFetched.Should().Be(1);
        result.Intervals[0].Error.Should().BeNull();
        requestStarts.Should().HaveCountGreaterThanOrEqualTo(5);
        requestStarts.Should().Contain(start => start > intervalMs * 4);
    }

    [TestMethod]
    public async Task GivenTransientFailure_WhenIngest_ThenRetriesAndContinues()
    {
        const long requestStartTime = 1700000000000L;
        const long intervalMs = 900_000L;
        var callCount = 0;

        _sut = CreateSut(maxRetries: 1, pageSize: 2);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "15m", It.IsAny<long>(), It.IsAny<long?>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => throw new HttpRequestException("temporary failure"),
                    2 =>
                    [
                        new CandleSnapshotDto
                        {
                            Timestamp = requestStartTime,
                            Open = 1m,
                            High = 2m,
                            Low = 0.5m,
                            Close = 1.5m,
                            Volume = 10m,
                            NumTrades = 5,
                        },
                    ],
                    _ => Array.Empty<CandleSnapshotDto>(),
                };
            });

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["15m"],
            StartTime = requestStartTime,
            EndTime = requestStartTime + (intervalMs * 2),
        });

        callCount.Should().Be(3);
        result.TotalFetched.Should().Be(1);
        result.Intervals[0].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenTimeoutExceeded_WhenIngest_ThenReturnsCancelledInterval()
    {
        _sut = CreateSut(maxIngestionTimeoutMs: 50);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", It.IsAny<long>(), It.IsAny<long?>(), 1500, It.IsAny<CancellationToken>()))
            .Returns<string, string, long, long?, int, CancellationToken>(async (_, _, _, _, _, token) =>
            {
                await Task.Delay(500, token);
                return [];
            });

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["1h"],
            StartTime = 1700000000000L,
            EndTime = 1700003600000L,
        });

        result.Intervals.Should().ContainSingle();
        result.Intervals[0].Error.Should().Be("Cancelled or timed out");
    }

    [TestMethod]
    public async Task GivenConcurrentRequests_WhenIngest_ThenThrowsIngestionAlreadyRunningException()
    {
        var releaseFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "1h", It.IsAny<long>(), It.IsAny<long?>(), 1500, It.IsAny<CancellationToken>()))
            .Returns<string, string, long, long?, int, CancellationToken>(async (_, _, _, _, _, token) =>
            {
                await releaseFirstCall.Task.WaitAsync(token);
                return [];
            });

        var firstCall = _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["1h"],
            StartTime = 1700000000000L,
            EndTime = 1700003600000L,
        });

        await Task.Delay(50);

        var secondCall = () => _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["1h"],
            StartTime = 1700000000000L,
            EndTime = 1700003600000L,
        });

        await secondCall.Should().ThrowAsync<IngestionAlreadyRunningException>()
            .WithMessage("Binance candle ingestion is already running.");

        releaseFirstCall.SetResult();
        await firstCall;
    }

    [TestMethod]
    public async Task GivenIncludeMarkPriceTrue_WhenIngestAsync_ThenFetchesAndStoresMarkPriceKlines()
    {
        const long requestStartTime = 1700000000000L;
        const long intervalMs = 900_000L;
        var persistedBatches = new List<IReadOnlyList<Candle>>();

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Candle>, CancellationToken>((candles, _) => persistedBatches.Add(candles.ToList()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "15m", requestStartTime, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CandleSnapshotDto
                {
                    Timestamp = requestStartTime,
                    Open = 1m,
                    High = 2m,
                    Low = 0.5m,
                    Close = 1.5m,
                    Volume = 10m,
                    NumTrades = 5,
                },
            ]);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "15m", requestStartTime + 1, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CandleSnapshotDto>());

        _restClientMock
            .Setup(client => client.GetMarkPriceKlinesAsync("BTCUSDT", "15m", requestStartTime, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CandleSnapshotDto
                {
                    Timestamp = requestStartTime,
                    Open = 0.9m,
                    High = 1.9m,
                    Low = 0.4m,
                    Close = 1.4m,
                    Volume = 0m,
                    NumTrades = 0,
                },
            ]);

        _restClientMock
            .Setup(client => client.GetMarkPriceKlinesAsync("BTCUSDT", "15m", requestStartTime + 1, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CandleSnapshotDto>());

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["15m"],
            StartTime = requestStartTime,
            EndTime = requestStartTime + intervalMs + 1,
            IncludeMarkPrice = true,
        });

        result.TotalFetched.Should().Be(2);
        result.TotalInserted.Should().Be(2);
        result.Intervals.Select(interval => interval.Interval).Should().Equal("15m", "mark-15m");
        persistedBatches.Should().HaveCount(2);
        persistedBatches[0].Single().Interval.Should().Be("15m");
        persistedBatches[1].Single().Interval.Should().Be("mark-15m");

        _restClientMock.Verify(
            client => client.GetMarkPriceKlinesAsync("BTCUSDT", "15m", It.IsAny<long>(), It.IsAny<long?>(), 1500, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task GivenIncludeMarkPriceFalse_WhenIngestAsync_ThenSkipsMarkPriceKlines()
    {
        const long requestStartTime = 1700000000000L;
        const long intervalMs = 900_000L;

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "15m", requestStartTime, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new CandleSnapshotDto
                {
                    Timestamp = requestStartTime,
                    Open = 1m,
                    High = 2m,
                    Low = 0.5m,
                    Close = 1.5m,
                    Volume = 10m,
                    NumTrades = 5,
                },
            ]);

        _restClientMock
            .Setup(client => client.GetKlinesAsync("BTCUSDT", "15m", requestStartTime + 1, requestStartTime + intervalMs + 1, 1500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CandleSnapshotDto>());

        var result = await _sut.IngestAsync(new IngestionRequest
        {
            Symbol = "BTC",
            Intervals = ["15m"],
            StartTime = requestStartTime,
            EndTime = requestStartTime + intervalMs + 1,
            IncludeMarkPrice = false,
        });

        result.TotalFetched.Should().Be(1);
        result.Intervals.Select(interval => interval.Interval).Should().Equal("15m");

        _restClientMock.Verify(
            client => client.GetMarkPriceKlinesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private BinanceCandleIngestionService CreateSut(
        int batchDelayMs = 0,
        int maxRetries = 3,
        int maxIngestionTimeoutMs = 7_200_000,
        int pageSize = 1500)
    {
        var options = Options.Create(new BinanceIngestionOptions
        {
            BatchDelayMs = batchDelayMs,
            MaxRetries = maxRetries,
            MaxIngestionTimeoutMs = maxIngestionTimeoutMs,
            DefaultStartDate = DefaultStartDate,
            PageSize = pageSize,
        });

        return new BinanceCandleIngestionService(
            _restClientMock.Object,
            _repositoryMock.Object,
            options,
            _loggerMock.Object);
    }
}