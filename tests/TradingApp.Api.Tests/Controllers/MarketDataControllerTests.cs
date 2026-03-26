using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.MarketData.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class MarketDataControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/market";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<IHyperliquidRestClient> _restClientMock = new();

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
            .Setup(c => c.GetMarketInfoAsync("BTC-PERP", It.IsAny<CancellationToken>()))
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
            .Setup(c => c.GetMarketInfoAsync("FAKE-PERP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketInfoDto?)null);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/info?asset=FAKE-PERP");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithValidParams_ThenReturnsOk()
    {
        var expected = new List<CandleDto>
        {
            new() { Timestamp = 1700000000000, Open = 50000m, High = 50100m, Low = 49900m, Close = 50050m, Volume = 100m },
            new() { Timestamp = 1699999100000, Open = 49900m, High = 50000m, Low = 49800m, Close = 49950m, Volume = 90m },
        };

        _restClientMock
            .Setup(c => c.GetCandlesAsync("BTC-PERP", "15m", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=15m");

        var result = await response.ReadAndAssertSuccessAsync<List<CandleDto>>();
        result.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task GivenController_WhenGetCandlesWithInvalidTimeframe_ThenReturnsBadRequest()
    {
        _restClientMock
            .Setup(c => c.GetCandlesAsync("BTC-PERP", "invalid", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Invalid timeframe 'invalid'. Supported: 15m, 1h, 4h"));

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/candles?asset=BTC-PERP&timeframe=invalid");

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }
}