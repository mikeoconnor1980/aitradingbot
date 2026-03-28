using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.FundingRates.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Services;

namespace TradingApp.Api.Tests.Services;

[TestClass]
public sealed class FundingRateIngestionServiceTests
{
    private static readonly DateTime DefaultStartDate = new(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private Mock<IBinanceFuturesRestClient> _restClientMock = default!;
    private Mock<IFundingRateRepository> _repositoryMock = default!;
    private Mock<ILogger<FundingRateIngestionService>> _loggerMock = default!;
    private FundingRateIngestionService _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IBinanceFuturesRestClient>(MockBehavior.Strict);
        _repositoryMock = new Mock<IFundingRateRepository>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<FundingRateIngestionService>>();
        _sut = CreateSut();
    }

    [TestMethod]
    public async Task GivenExistingFundingRates_WhenIngest_ThenResumesFromLatestTimestamp()
    {
        const long latestTimestamp = 1700000000000L;
        const long endTime = latestTimestamp + 1000;

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestTimestamp);

        _restClientMock
            .Setup(client => client.GetFundingRatesAsync("BTCUSDT", latestTimestamp + 1, endTime, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.IngestAsync(new FundingRateIngestionRequest
        {
            Symbol = "BTC",
            EndTime = endTime,
        });

        result.Symbol.Should().Be("BTC");

        _restClientMock.Verify(
            client => client.GetFundingRatesAsync("BTCUSDT", latestTimestamp + 1, endTime, 1000, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenPagedResponses_WhenIngest_ThenSortsPersistsAndAggregatesResults()
    {
        const long startTime = 1700000000000L;
        const long endTime = startTime + 2_000_000L;

        var firstBatch = Enumerable.Range(0, 1000)
            .Select(index => new FundingRateDto
            {
                FundingTime = startTime + (index * 1000L),
                FundingRate = 0.0001m + (index * 0.000001m),
                MarkPrice = 50000m + index,
            })
            .ToList();

        (firstBatch[0], firstBatch[1]) = (firstBatch[1], firstBatch[0]);

        var batches = new Queue<IReadOnlyList<FundingRateDto>>([
            firstBatch,
            new List<FundingRateDto>
            {
                new() { FundingTime = startTime + 1_000_000L, FundingRate = 0.0012m, MarkPrice = 51000m },
            },
        ]);

        var persistedBatches = new List<IReadOnlyList<FundingRate>>();

        _restClientMock
            .Setup(client => client.GetFundingRatesAsync("BTCUSDT", It.IsAny<long>(), endTime, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => batches.Dequeue());

        _repositoryMock
            .Setup(repository => repository.BulkInsertAsync(It.IsAny<IEnumerable<FundingRate>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<FundingRate>, CancellationToken>((fundingRates, _) => persistedBatches.Add(fundingRates.ToList()))
            .Returns(Task.CompletedTask);

        var result = await _sut.IngestAsync(new FundingRateIngestionRequest
        {
            Symbol = "BTC",
            StartTime = startTime,
            EndTime = endTime,
        });

        result.TotalFetched.Should().Be(1001);
        result.TotalInserted.Should().Be(1001);
        result.TotalSkipped.Should().Be(0);
        result.EarliestTimestamp.Should().Be("2023-11-14 22:13:20 UTC");
        result.LatestTimestamp.Should().Be("2023-11-14 22:30:00 UTC");
        persistedBatches.Should().HaveCount(2);
        persistedBatches[0].Select(rate => rate.Timestamp).Should().BeInAscendingOrder();
        persistedBatches[0][0].Timestamp.Should().Be(startTime);
        persistedBatches[0][1].Timestamp.Should().Be(startTime + 1000);
        persistedBatches.SelectMany(batch => batch).Should().OnlyContain(rate => rate.Symbol == "BTC");
    }

    [TestMethod]
    public async Task GivenTimeoutExceeded_WhenIngest_ThenReturnsCancelledResult()
    {
        _sut = CreateSut(maxIngestionTimeoutMs: 50);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetFundingRatesAsync("BTCUSDT", It.IsAny<long>(), It.IsAny<long?>(), 1000, It.IsAny<CancellationToken>()))
            .Returns<string, long, long?, int, CancellationToken>(async (_, _, _, _, token) =>
            {
                await Task.Delay(500, token);
                return [];
            });

        var result = await _sut.IngestAsync(new FundingRateIngestionRequest
        {
            Symbol = "BTC",
        });

        result.Error.Should().Be("Cancelled or timed out");
    }

    [TestMethod]
    public async Task GivenConcurrentRequests_WhenIngest_ThenThrowsIngestionAlreadyRunningException()
    {
        var releaseFirstCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _repositoryMock
            .Setup(repository => repository.GetLatestTimestampAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        _restClientMock
            .Setup(client => client.GetFundingRatesAsync("BTCUSDT", It.IsAny<long>(), It.IsAny<long?>(), 1000, It.IsAny<CancellationToken>()))
            .Returns<string, long, long?, int, CancellationToken>(async (_, _, _, _, token) =>
            {
                await releaseFirstCall.Task.WaitAsync(token);
                return [];
            });

        var firstCall = _sut.IngestAsync(new FundingRateIngestionRequest
        {
            Symbol = "BTC",
        });

        await Task.Delay(50);

        var secondCall = () => _sut.IngestAsync(new FundingRateIngestionRequest
        {
            Symbol = "BTC",
        });

        await secondCall.Should().ThrowAsync<IngestionAlreadyRunningException>()
            .WithMessage("Funding rate ingestion is already running.");

        releaseFirstCall.SetResult();
        await firstCall;
    }

    private FundingRateIngestionService CreateSut(
        int batchDelayMs = 0,
        int maxIngestionTimeoutMs = 7_200_000)
    {
        var options = Options.Create(new BinanceIngestionOptions
        {
            BatchDelayMs = batchDelayMs,
            MaxIngestionTimeoutMs = maxIngestionTimeoutMs,
            DefaultStartDate = DefaultStartDate,
            PageSize = 1500,
        });

        return new FundingRateIngestionService(
            _restClientMock.Object,
            _repositoryMock.Object,
            options,
            _loggerMock.Object);
    }
}