using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class FundingRatesControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/funding";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<IFundingRateIngestionService> _ingestionServiceMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IFundingRateIngestionService>();
        services.AddSingleton(_ingestionServiceMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostIngestFunding_ThenReturnsOkWithResult()
    {
        var expectedResult = new FundingRateIngestionResult
        {
            Symbol = "BTC",
            TotalFetched = 1000,
            TotalInserted = 995,
            TotalSkipped = 5,
            ElapsedMs = 5000,
            EarliestTimestamp = "2019-09-01 00:00:00 UTC",
            LatestTimestamp = "2026-03-28 00:00:00 UTC",
        };

        _ingestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<FundingRateIngestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new
            {
                symbol = "BTC",
                startTime = 1700000000000L,
                endTime = 1700003600000L,
            }));

        var result = await response.ReadAndAssertSuccessAsync<FundingRateIngestionResult>();

        result.Symbol.Should().Be("BTC");
        result.TotalFetched.Should().Be(1000);
        result.TotalInserted.Should().Be(995);
        result.TotalSkipped.Should().Be(5);

        _ingestionServiceMock.Verify(
            service => service.IngestAsync(
                It.Is<FundingRateIngestionRequest>(request =>
                    request.Symbol == "BTC" &&
                    request.StartTime == 1700000000000L &&
                    request.EndTime == 1700003600000L),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenPostIngestFunding_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { startTime = 1700000000000L }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenInvalidSymbol_WhenPostIngestFunding_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "INVALID" }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Invalid symbol: 'INVALID'. Valid Binance symbols");
        body.GetProperty("errorMessage").GetString().Should().Contain(string.Join(", ", BinanceAssetMapper.ValidSymbols));
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenConcurrentIngestion_WhenPostIngestFunding_ThenReturnsConflict()
    {
        _ingestionServiceMock
            .Setup(service => service.IngestAsync(It.IsAny<FundingRateIngestionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IngestionAlreadyRunningException("Funding rate ingestion is already running."));

        var client = GetTestClient();

        var response = await client.PostAsync(
            $"{BaseUrl}/ingest",
            GetStringContent(new { symbol = "BTC" }));

        response.AssertStatusCode(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("ingestion_conflict");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }
}