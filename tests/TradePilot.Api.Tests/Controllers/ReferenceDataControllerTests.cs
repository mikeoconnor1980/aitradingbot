using Microsoft.AspNetCore.Hosting;
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class ReferenceDataControllerTests : BaseControllerTests
{
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    [TestMethod]
    public async Task GivenMarketsEndpoint_WhenGet_ThenReturnsMarketsAndTimeframes()
    {
        var client = GetTestClient();

        var response = await client.GetAsync("api/reference-data/markets");

        var body = await response.ReadAndAssertSuccessAsync<ReferenceDataResponse>();
        body.Markets.Should().NotBeEmpty();
        body.Markets.Should().OnlyContain(market => market != null && market.EndsWith("-PERP"));
        body.Markets.Should().Contain("BTC-PERP");
        body.Markets.Should().NotContain("IBTC-PERP");
        body.Timeframes.Should().Contain("15m");
    }
}