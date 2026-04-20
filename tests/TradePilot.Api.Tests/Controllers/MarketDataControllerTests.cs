using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class MarketDataControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/market";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<IHyperliquidRestClient> _restClientMock = new();
    private readonly Mock<ICandleRepository> _candleRepositoryMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHyperliquidRestClient>();
        services.AddSingleton(_restClientMock.Object);

        services.RemoveAll<ICandleRepository>();
        services.AddSingleton(_candleRepositoryMock.Object);

        services.RemoveAll<IHostedService>();
    }

    [TestMethod]
    public async Task GivenController_WhenGetMarketInfoWithValidAsset_ThenReturnsOk()
    {
        var expected = new MarketInfoDto
        {
            Asset = "BTC-PERP",
            MidPrice = 50000m,
            MarkPrice = 50001m,
            IndexPrice = 49999m,
            FundingRate = 0.0001m,
            Volume24h = 1_000_000m,
            OpenInterest = 500_000m,
            PriceChange24hPercent = 2.5m,
        };

        _restClientMock
            .Setup(c => c.GetMarketInfoAsync("BTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/info?asset=BTC-PERP");

        var result = await response.ReadAndAssertSuccessAsync<MarketInfoDto>();
        result.Asset.Should().Be("BTC-PERP");
        result.MidPrice.Should().Be(50000m);
    }

    [TestMethod]
    public async Task GivenController_WhenGetMarketInfoReturnsNull_ThenReturnsNotFound()
    {
        _restClientMock
            .Setup(c => c.GetMarketInfoAsync("FAKE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/info?asset=FAKE-PERP");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithValidParams_ThenReturnsOk()
    {
        var expected = Enumerable.Range(0, 60)
            .Select(index => new CandleSnapshotDto
            {
                Timestamp = 1_700_000_000_000 - (index * 900_000L),
                Open = 50000m + index,
                High = 50100m + index,
                Low = 49900m + index,
                Close = 50050m + index,
                Volume = 100m + index,
            })
            .ToList();

        _restClientMock
            .Setup(c => c.GetCandleSnapshotsAsync(
                "BTC",
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=15m");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().HaveCount(60);
        result[0].Indicators.Should().NotBeNull();
        result[0].Indicators!.EmaFast.Should().NotBeNull();
        result[0].Indicators!.MacdLine.Should().NotBeNull();
        result[0].Indicators!.BollingerUpper.Should().NotBeNull();
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithInvalidTimeframe_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=invalid");

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenHistoricalCandlesExist_WhenGetHistoricalCandles_ThenReturnsOkWithCandleData()
    {
        var candles = new List<Candle>
        {
            Candle.Create("Binance", "BTC", "15m", 1_700_000_000_000, 50000m, 50100m, 49900m, 50050m, 100m, 10),
            Candle.Create("Binance", "BTC", "15m", 1_700_000_900_000, 50050m, 50200m, 50000m, 50150m, 125m, 12),
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

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP&timeframe=15m&limit=500");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(1_700_000_000_000);
        result[1].Close.Should().Be(50150m);
    }

    [TestMethod]
    public async Task GivenNoHistoricalCandles_WhenGetHistoricalCandles_ThenReturnsOkWithEmptyArray()
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

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP&timeframe=15m");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenMoreHistoricalCandlesThanRequested_WhenGetHistoricalCandles_ThenRespectsLimitParameter()
    {
        var candles = Enumerable.Range(0, 1000)
            .Select(index => Candle.Create(
                "Binance",
                "BTC",
                "15m",
                1_700_000_000_000 + (index * 900_000L),
                50000m,
                50100m,
                49900m,
                50050m,
                100m,
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

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP&timeframe=15m&limit=100");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().HaveCount(100);
        result[0].Timestamp.Should().Be(candles[900].Timestamp);
        result[^1].Timestamp.Should().Be(candles[^1].Timestamp);
    }

    [TestMethod]
    public async Task GivenEndTimeParameter_WhenGetHistoricalCandles_ThenUsesRequestedEndTimeRange()
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
            .ReturnsAsync(new[]
            {
                Candle.Create("Binance", "BTC", "15m", expectedStartTime, 1m, 2m, 1m, 2m, 3m, 4)
            });

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP&timeframe=15m&endTime={endTime}&limit={limit}");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().ContainSingle();

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
    public async Task GivenMissingAsset_WhenGetHistoricalCandles_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?timeframe=15m");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("asset", out _).Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenMissingTimeframe_WhenGetHistoricalCandles_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("timeframe", out _).Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenGetHistoricalCandles_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles/history?asset=BTC-PERP&timeframe=2m");

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }
}