using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Models;
using TradingApp.Api.Tests.Infrastructure;

namespace TradingApp.Api.Tests.Controllers;

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

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();
    }

    [TestMethod]
    public async Task GivenMarketsEndpoint_WhenGet_ThenReturnsMarketsAndTimeframes()
    {
        var client = GetTestClient();

        var response = await client.GetAsync("api/reference-data/markets");

        var body = await response.ReadAndAssertSuccessAsync<ReferenceDataResponse>();
        body.Markets.Should().NotBeEmpty();
        body.Markets.Should().OnlyContain(market => market != null && market.EndsWith("-USD"));
        body.Markets.Should().Contain("BTC-USD");
        body.Markets.Should().NotContain("BTC-PERP");
        body.Timeframes.Should().Contain("15m");
    }
}