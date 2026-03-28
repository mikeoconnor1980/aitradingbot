using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class CandlesControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/candles";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<ICandleIngestionService> _ingestionServiceMock = new();
    private readonly Mock<IBinanceCandleIngestionService> _binanceIngestionServiceMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ICandleIngestionService>();
        services.AddSingleton(_ingestionServiceMock.Object);

        services.RemoveAll<IBinanceCandleIngestionService>();
        services.AddSingleton(_binanceIngestionServiceMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostIngest_ThenReturnsOkWithResult()
    {
        var expectedResult = new IngestionResult
        {
            TotalFetched = 1000,
            TotalInserted = 995,
            TotalSkipped = 5,
            ElapsedMs = 5000,
            Intervals =
            [
                new IntervalResult
                {
                    Interval = "1h",
                    Fetched = 1000,
                    Inserted = 995,
                    Skipped = 5,
                },
            ],
        };

        _ingestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new
            {
                symbol = "BTC",
                intervals = new[] { "1h" },
                startTime = 1700000000000L,
                endTime = 1700003600000L,
            }));

        var result = await response.ReadAndAssertSuccessAsync<IngestionResult>();

        result.TotalFetched.Should().Be(1000);
        result.TotalInserted.Should().Be(995);
        result.TotalSkipped.Should().Be(5);
        result.Intervals.Should().ContainSingle();

        _ingestionServiceMock.Verify(
            service => service.IngestAsync(
                It.Is<IngestionRequest>(request =>
                    request.Symbol == "BTC" &&
                    request.Intervals.SequenceEqual(new[] { "1h" }) &&
                    request.StartTime == 1700000000000L &&
                    request.EndTime == 1700003600000L),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenPostIngest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { intervals = new[] { "1h" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenEmptyIntervals_WhenPostIngest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = Array.Empty<string>() }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenUnknownSymbol_WhenPostIngest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "FAKE", intervals = new[] { "1h" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Unknown symbol 'FAKE'");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenInvalidInterval_WhenPostIngest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "invalid" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Supported: 5m, 15m, 1h, 4h");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenConcurrentIngestion_WhenPostIngest_ThenReturnsConflict()
    {
        _ingestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionAlreadyRunningException());

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "1h" } }));

        response.AssertStatusCode(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("ingestion_conflict");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostIngestBinance_ThenReturnsOkWithResult()
    {
        var expectedResult = new IngestionResult
        {
            TotalFetched = 1000,
            TotalInserted = 1000,
            TotalSkipped = 0,
            ElapsedMs = 5000,
            Intervals =
            [
                new IntervalResult
                {
                    Interval = "15m",
                    Fetched = 1000,
                    Inserted = 1000,
                    Skipped = 0,
                    EarliestCandle = "2019-09-01 00:00:00 UTC",
                    LatestCandle = "2019-09-11 09:45:00 UTC",
                },
            ],
        };

        _binanceIngestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new
            {
                symbol = "BTC",
                intervals = new[] { "15m", "1h" },
                startTime = 1700000000000L,
                endTime = 1700003600000L,
            }));

        var result = await response.ReadAndAssertSuccessAsync<IngestionResult>();

        result.TotalFetched.Should().Be(1000);
        result.TotalInserted.Should().Be(1000);
        result.Intervals.Should().ContainSingle();

        _binanceIngestionServiceMock.Verify(
            service => service.IngestAsync(
                It.Is<IngestionRequest>(request =>
                    request.Symbol == "BTC" &&
                    request.Intervals.SequenceEqual(new[] { "15m", "1h" }) &&
                    request.StartTime == 1700000000000L &&
                    request.EndTime == 1700003600000L),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenPostIngestBinance_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new { intervals = new[] { "15m" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenEmptyIntervals_WhenPostIngestBinance_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new { symbol = "BTC", intervals = Array.Empty<string>() }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenInvalidSymbol_WhenPostIngestBinance_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new { symbol = "INVALID", intervals = new[] { "15m" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Invalid symbol: 'INVALID'. Valid Binance symbols");
        body.GetProperty("errorMessage").GetString().Should().Contain(string.Join(", ", BinanceAssetMapper.ValidSymbols));
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenInvalidInterval_WhenPostIngestBinance_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "3m" } }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Invalid interval: '3m'. Valid Binance intervals");
        body.GetProperty("errorMessage").GetString().Should().Contain(string.Join(", ", BinanceAssetMapper.ValidIntervals));
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenConcurrentIngestion_WhenPostIngestBinance_ThenReturnsConflict()
    {
        _binanceIngestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionAlreadyRunningException("Already running"));

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new { symbol = "BTC", intervals = new[] { "15m" } }));

        response.AssertStatusCode(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("ingestion_conflict");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenIncludeMarkPriceTrue_WhenPostIngestBinance_ThenPassesFlagToIngestionRequest()
    {
        _binanceIngestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<IngestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngestionResult
            {
                TotalFetched = 2,
                TotalInserted = 2,
                TotalSkipped = 0,
                ElapsedMs = 10,
                Intervals =
                [
                    new IntervalResult { Interval = "15m", Fetched = 1, Inserted = 1, Skipped = 0 },
                    new IntervalResult { Interval = "mark-15m", Fetched = 1, Inserted = 1, Skipped = 0 },
                ],
            });

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest/binance",
            GetStringContent(new
            {
                symbol = "BTC",
                intervals = new[] { "15m" },
                includeMarkPrice = true,
            }));

        var result = await response.ReadAndAssertSuccessAsync<IngestionResult>();

        result.Intervals.Select(interval => interval.Interval).Should().Equal("15m", "mark-15m");
        _binanceIngestionServiceMock.Verify(
            service => service.IngestAsync(
                It.Is<IngestionRequest>(request =>
                    request.Symbol == "BTC"
                    && request.Intervals.SequenceEqual(new[] { "15m" })
                    && request.IncludeMarkPrice),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}