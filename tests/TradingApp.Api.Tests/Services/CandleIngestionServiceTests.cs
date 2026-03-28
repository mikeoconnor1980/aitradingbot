using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Application.MarketData.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class CandleIngestionServiceTests
{
    private static readonly DateTime DefaultStartDate = new(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int PageSize = 500;

    private Mock<IHyperliquidRestClient> _restClientMock = default!;
    private Mock<ICandleRepository> _repositoryMock = default!;
    private Mock<ILogger<CandleIngestionService>> _loggerMock = default!;
    private CandleIngestionService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>(MockBehavior.Strict);
        _repositoryMock = new Mock<ICandleRepository>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<CandleIngestionService>>();
        _sut = CreateSut();
    }

    [TestMethod]
    public async Task GivenEmptyDatabase_WhenIngest_ThenFetchesFromDefaultStartDate()
    {
        var expectedStart = new DateTimeOffset(DefaultStartDate).ToUnixTimeMilliseconds();
        var explicitEndTime = expectedStart + 3600000L;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", expectedStart, explicitEndTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"], EndTime = explicitEndTime });

        result.TotalFetched.Should().Be(0);
        result.TotalInserted.Should().Be(0);
        result.Intervals.Should().ContainSingle();
        result.Intervals[0].Interval.Should().Be("1h");
        result.Intervals[0].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenExistingCandles_WhenIngest_ThenResumesFromLatestTimestamp()
    {
        const long latestTimestamp = 1700000000000L;
        const long explicitEndTime = latestTimestamp + 3600000L + 1;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestTimestamp);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", latestTimestamp + 1, explicitEndTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"], EndTime = explicitEndTime });

        _restClientMock.Verify(
            client => client.GetCandleSnapshotsAsync("BTC", "1h", latestTimestamp + 1, explicitEndTime, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenPagedBatch_WhenIngest_ThenSortsMapsAndPersistsCandles()
    {
        const long firstTimestamp = 1700000000000L;
        var fullBatch = Enumerable.Range(0, PageSize)
            .Select(index => new CandleSnapshotDto
            {
                Timestamp = firstTimestamp + (index * 3600000L),
                Open = 50000m + index,
                High = 50010m + index,
                Low = 49990m + index,
                Close = 50005m + index,
                Volume = 100m + index,
                NumTrades = 10 + index,
            })
            .Reverse()
            .ToList();

        var secondBatch = new List<CandleSnapshotDto>
        {
            new()
            {
                Timestamp = firstTimestamp + (PageSize * 3600000L),
                Open = 55000m,
                High = 55100m,
                Low = 54900m,
                Close = 55050m,
                Volume = 250m,
                NumTrades = 45,
            },
        };

        IReadOnlyList<Candle>? persistedBatch = null;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Candle>, CancellationToken>((candles, _) => persistedBatch = candles.ToList())
            .Returns(Task.CompletedTask);

        var callCount = 0;
        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? fullBatch : secondBatch;
            });

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] });

        result.TotalFetched.Should().Be(PageSize + 1);
        result.TotalInserted.Should().Be(PageSize + 1);
        result.TotalSkipped.Should().Be(0);
        _repositoryMock.Verify(
            repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        persistedBatch.Should().NotBeNull();
        persistedBatch!.First().Timestamp.Should().Be(secondBatch[0].Timestamp);
    }

    [TestMethod]
    public async Task GivenMultipleFullBatches_WhenIngest_ThenAppliesConfiguredBatchDelay()
    {
        var invocationTimes = new List<DateTimeOffset>();
        var explicitEndTime = 1700000000000L + (PageSize * 3600000L) + 1;
        var batch = Enumerable.Range(0, PageSize)
            .Select(index => new CandleSnapshotDto
            {
                Timestamp = 1700000000000L + (index * 3600000L),
                Open = 1m,
                High = 2m,
                Low = 1m,
                Close = 2m,
                Volume = 1m,
                NumTrades = 1,
            })
            .ToList();

        _sut = CreateSut(batchDelayMs: 100);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callCount = 0;
        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                invocationTimes.Add(DateTimeOffset.UtcNow);
                callCount++;
                return callCount == 1 ? batch : [];
            });

    await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"], EndTime = explicitEndTime });

        invocationTimes.Should().HaveCount(2);
        (invocationTimes[1] - invocationTimes[0]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90));
    }

    [TestMethod]
    public async Task GivenRequestWithoutExplicitEndTime_WhenIngest_ThenStopsAtLastClosedCandleBoundary()
    {
        var latestTimestamp = DateTimeOffset.UtcNow.AddHours(-8).ToUnixTimeMilliseconds();

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "4h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestTimestamp);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "4h", latestTimestamp + 1, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, long, long, CancellationToken>((_, _, _, endTime, _) =>
            {
                endTime.Should().BeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddHours(-4).ToUnixTimeMilliseconds());
                return Task.FromResult(new List<CandleSnapshotDto>());
            });

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["4h"] });

        result.TotalFetched.Should().Be(0);
        result.Intervals[0].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenNonAdvancingBatch_WhenIngest_ThenStopsToAvoidInfiniteLoop()
    {
        const long latestTimestamp = 1700000000000L;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "4h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestTimestamp);

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "4h", latestTimestamp + 1, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new CandleSnapshotDto
                {
                    Timestamp = latestTimestamp,
                    Open = 1m,
                    High = 1m,
                    Low = 1m,
                    Close = 1m,
                    Volume = 1m,
                    NumTrades = 1,
                },
            ]);

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["4h"] });

        result.TotalFetched.Should().Be(0);
        result.TotalInserted.Should().Be(0);
        _repositoryMock.Verify(
            repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<Candle>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenOneIntervalFails_WhenIngest_ThenOtherIntervalsContinue()
    {
        _sut = CreateSut(maxRetries: 0);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "15m", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);
        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));
        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["15m", "1h"] });

        result.Intervals.Should().HaveCount(2);
        result.Intervals[0].Error.Should().Be("Network error");
        result.Intervals[1].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenTimeoutExceeded_WhenIngest_ThenReturnsCancelledInterval()
    {
        _sut = CreateSut(maxIngestionTimeoutMs: 50);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, long, long, CancellationToken>(async (_, _, _, _, token) =>
            {
                await Task.Delay(500, token);
                return [];
            });

        var result = await _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] });

        result.Intervals.Should().ContainSingle();
        result.Intervals[0].Error.Should().Be("Cancelled or timed out");
    }

    [TestMethod]
    public async Task GivenConcurrentRequests_WhenIngest_ThenThrowsIngestionAlreadyRunningException()
    {
        var releaseFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, long, long, CancellationToken>(async (_, _, _, _, token) =>
            {
                await releaseFirstCall.Task.WaitAsync(token);
                return [];
            });

        var firstCall = _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] });
        await Task.Delay(50);

        var secondCall = () => _sut.IngestAsync(new IngestionRequest { Symbol = "BTC", Intervals = ["1h"] });

        await secondCall.Should().ThrowAsync<IngestionAlreadyRunningException>();

        releaseFirstCall.SetResult();
        await firstCall;
    }

    private CandleIngestionService CreateSut(
        int batchDelayMs = 0,
        int maxRetries = 3,
        int maxIngestionTimeoutMs = 900000)
    {
        var options = Options.Create(new CandleIngestionOptions
        {
            BatchDelayMs = batchDelayMs,
            MaxRetries = maxRetries,
            MaxIngestionTimeoutMs = maxIngestionTimeoutMs,
            DefaultStartDate = DefaultStartDate,
        });

        return new CandleIngestionService(
            _restClientMock.Object,
            _repositoryMock.Object,
            options,
            _loggerMock.Object);
    }
}