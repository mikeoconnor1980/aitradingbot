using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class StrategyReviewTests : BaseControllerTests
{
    private const string BaseUrl = "/api/strategies";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";
    private const string ValidGridConfigJsonTemplate = """
    {
        "schemaVersion": 1,
        "strategyMode": "grid",
        "strategyName": "__NAME__",
        "exchange": "Hyperliquid",
        "market": "BTC-USD",
        "timeframe": "15m",
        "direction": "long",
        "enabled": true,
        "templateId": "grid",
        "grid": { "levels": 10, "spacing": 0.5, "entryMode": "auto_from_signal_candle", "breakdownThreshold": 1.5 },
        "exit": {
            "takeProfit": { "enabled": true, "type": "fixed_percent", "value": 2.0 },
            "stopLoss": { "enabled": true, "type": "fixed_percent", "value": 6.0 },
            "exitOnOppositeSignal": false
        },
        "risk": {
            "positionSizeType": "percent_wallet",
            "positionSizeValue": 5.0,
            "leverage": 1.0,
            "maxOpenTrades": 1,
            "cooldownValue": 0,
            "cooldownUnit": "candles",
            "allowSameCandleReentry": false
        }
    }
    """;

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"tradingapp-review-tests-{Guid.NewGuid():N}.db");

    private Mock<ILlmClient> _llmClientMock = default!;
    private Mock<IReviewLlmClient> _reviewLlmClientMock = default!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
        builder.UseSetting("LlmReview:Provider", "Gemini");
        builder.UseSetting("LlmReview:BaseUrl", "https://example.test/openai/");
        builder.UseSetting("LlmReview:ModelName", "test-review-model");
        builder.UseSetting("LlmReview:ApiKey", "test-review-api-key");
        builder.UseSetting("LlmReview:TimeoutSeconds", "30");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();

        _llmClientMock = new Mock<ILlmClient>();
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");
        services.RemoveAll<ILlmClient>();
        services.AddSingleton(_llmClientMock.Object);

        _reviewLlmClientMock = new Mock<IReviewLlmClient>();
        _reviewLlmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("## 1. Strategy Summary\n- This is a grid strategy.");
        services.RemoveAll<IReviewLlmClient>();
        services.AddSingleton(_reviewLlmClientMock.Object);
    }

    [TestMethod]
    public async Task GivenSavedStrategy_WhenReviewRequested_ThenReturns200WithReview()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var response = await PostReviewAsync(client, strategyId, 1);

        var review = await response.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.StrategyId.Should().Be(strategyId);
        review.RevisionNumber.Should().Be(1);
        review.ReviewMarkdown.Should().Contain("Strategy Summary");
        review.ModelName.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task GivenReviewExists_WhenGetReview_ThenReturns200()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        await PostReviewAsync(client, strategyId, 1);

        var response = await client.GetAsync($"{BaseUrl}/{strategyId}/versions/1/review");

        var review = await response.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.ReviewMarkdown.Should().Contain("Strategy Summary");
    }

    [TestMethod]
    public async Task GivenNoReviewExists_WhenGetReview_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var response = await client.GetAsync($"{BaseUrl}/{strategyId}/versions/1/review");

        response.AssertStatusCode(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("not_found");
    }

    [TestMethod]
    public async Task GivenNonExistentStrategy_WhenReviewRequested_ThenReturns404()
    {
        var client = GetTestClient();

        var response = await PostReviewAsync(client, Guid.NewGuid(), 1);

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenUnknownRevision_WhenReviewRequested_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var response = await PostReviewAsync(client, strategyId, 99);

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenInvalidRevision_WhenReviewRequested_ThenReturns400()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var response = await PostReviewAsync(client, strategyId, 0);

        response.AssertStatusCode(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GivenReviewExists_WhenReviewRequestedAgain_ThenOverwritesPreviousReview()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var firstResponse = await PostReviewAsync(client, strategyId, 1, "203.0.113.21");
        firstResponse.AssertStatusCode(HttpStatusCode.OK);

        _reviewLlmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("## Updated review content");

        var secondResponse = await PostReviewAsync(client, strategyId, 1, "203.0.113.22");

        var review = await secondResponse.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        review.ReviewMarkdown.Should().Contain("Updated review content");

        var getResponse = await client.GetAsync($"{BaseUrl}/{strategyId}/versions/1/review");
        var storedReview = await getResponse.ReadAndAssertSuccessAsync<StrategyReviewDto>();
        storedReview.ReviewMarkdown.Should().Contain("Updated review content");
    }

    [TestMethod]
    public async Task GivenTwoRapidRequests_WhenReviewRequested_ThenSecondReturns429()
    {
        var client = GetTestClient();
        var strategyId = await CreateStrategyAsync(client);

        var firstResponse = await PostReviewAsync(client, strategyId, 1, "203.0.113.20");
        firstResponse.AssertStatusCode(HttpStatusCode.OK);

        var secondResponse = await PostReviewAsync(client, strategyId, 1, "203.0.113.20");

        secondResponse.AssertStatusCode(HttpStatusCode.TooManyRequests);
        secondResponse.Headers.RetryAfter.Should().NotBeNull();

        var body = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("rate_limit");
    }

    private static StringContent GetJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<Guid> CreateStrategyAsync(HttpClient client)
    {
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var json = ValidGridConfigJsonTemplate.Replace("__NAME__", strategyName, StringComparison.Ordinal);

        var response = await client.PostAsync(BaseUrl, GetJsonContent(json));
        response.AssertStatusCode(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PostReviewAsync(
        HttpClient client,
        Guid strategyId,
        int revisionNumber,
        string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/{strategyId}/versions/{revisionNumber}/review");

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request);
    }
}