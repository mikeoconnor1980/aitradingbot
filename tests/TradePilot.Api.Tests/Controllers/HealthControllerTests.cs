using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Health.Models;
using TradePilot.Infrastructure.Services;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class HealthControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/health";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private WebApplicationFactory<Program>? _localFactory;

    [TestMethod]
    public async Task GivenConnectedTestnet_WhenGetHealth_ThenReturnsConnectedStatus()
    {
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        var response = await client.GetAsync(BaseUrl);

        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("connected");
        health.Network.Should().Be("testnet");
        health.Error.Should().BeNull();
        health.WalletAddress.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenDisconnectedTestnet_WhenGetHealth_ThenReturnsDisconnectedStatus()
    {
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        var response = await client.GetAsync(BaseUrl);

        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("disconnected");
        health.Error.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenNetworkError_WhenGetHealth_ThenReturnsDisconnectedWithError()
    {
        var fakeHandler = new FakeHttpMessageHandler(
            new HttpRequestException("Network unreachable"));
        var client = CreateTestClientWithFakeHttp(fakeHandler);

        var response = await client.GetAsync(BaseUrl);

        var health = await response.ReadAndAssertSuccessAsync<HealthDto>();
        health.Status.Should().Be("disconnected");
        health.Error.Should().Contain("did not respond successfully");
    }

    [TestCleanup]
    public void CleanupLocal()
    {
        _localFactory?.Dispose();
        _localFactory = null;
    }

    private HttpClient CreateTestClientWithFakeHttp(FakeHttpMessageHandler fakeHandler)
    {
        _localFactory?.Dispose();
        _localFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.UseInMemoryTradePilotPersistence($"health-controller-tests-{Guid.NewGuid():N}");
                builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
                builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
                builder.UseSetting("Hyperliquid:Network", "testnet");
                builder.UseSetting("LlmReview:Provider", "Gemini");
                builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
                builder.UseSetting("LlmReview:ModelName", "test-review-model");
                builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
                builder.UseSetting("LlmReview:TimeoutSeconds", "30");

                builder.ConfigureServices(services =>
                {
                    services.AddHttpClient<IHyperliquidRestClient, HyperliquidRestClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
                });
            });

        return _localFactory.CreateClient();
    }
}
