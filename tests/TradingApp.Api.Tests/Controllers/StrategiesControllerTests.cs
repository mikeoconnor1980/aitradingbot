using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Api.Tests.Infrastructure;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class StrategiesControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/strategies";
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
        $"tradingapp-strategies-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();
    }

    [TestMethod]
    public async Task GivenValidGridConfig_WhenValidate_ThenReturnsIsValidTrue()
    {
        var client = GetTestClient();
        const string json = """
        {
            "schemaVersion": 1,
            "strategyMode": "grid",
            "strategyName": "BTC Grid",
            "exchange": "Hyperliquid",
            "market": "BTC-USD",
            "timeframe": "15m",
            "direction": "long",
            "enabled": true,
            "grid": { "levels": 10, "spacing": 0.5, "entryMode": "auto_from_signal_candle", "breakdownThreshold": 1.5 },
            "exit": {
                "takeProfit": { "enabled": true, "type": "fixed_percent", "value": 2 },
                "stopLoss": { "enabled": true, "type": "fixed_percent", "value": 6 }
            },
            "risk": { "positionSizeType": "percent_wallet", "positionSizeValue": 5, "leverage": 1, "maxOpenTrades": 1 }
        }
        """;

        var response = await client.PostAsync(
            "api/strategies/validate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenInvalidConfig_WhenValidate_ThenReturnsErrors()
    {
        var client = GetTestClient();
        const string json = """
        {
            "schemaVersion": 1,
            "strategyMode": "grid",
            "strategyName": "",
            "exchange": "Hyperliquid",
            "market": "BTC-USD",
            "grid": null,
            "exit": {
                "takeProfit": { "enabled": true, "type": "fixed_percent", "value": 2 },
                "stopLoss": { "enabled": true, "type": "fixed_percent", "value": 6 }
            },
            "risk": { "positionSizeType": "percent_wallet", "positionSizeValue": 5, "leverage": 1, "maxOpenTrades": 1 }
        }
        """;

        var response = await client.PostAsync(
            "api/strategies/validate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeFalse();
        body.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task GivenValidStrategyConfig_WhenCreate_ThenReturns201WithId()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(BaseUrl, GetJsonContent(CreateValidGridConfigJson()));

        response.AssertStatusCode(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [TestMethod]
    public async Task GivenExistingStrategies_WhenGetStrategies_ThenReturnsCurrentUserStrategies()
    {
        var client = GetTestClient();
        var firstName = $"Grid-{Guid.NewGuid():N}";
        var secondName = $"Grid-{Guid.NewGuid():N}";

        var firstId = await CreateStrategyAsync(client, firstName);
        var secondId = await CreateStrategyAsync(client, secondName);

        var response = await client.GetAsync(BaseUrl);

        var body = await response.ReadAndAssertSuccessAsync<List<StrategySummaryDto>>();

        body.Should().Contain(strategy => strategy.Id == firstId && strategy.Name == firstName);
        body.Should().Contain(strategy => strategy.Id == secondId && strategy.Name == secondName);
    }

    [TestMethod]
    public async Task GivenExistingStrategy_WhenGetById_ThenReturns200WithConfig()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{id}");

        var body = await response.ReadAndAssertSuccessAsync<StrategyDto>();
        body.Id.Should().Be(id);
        body.Name.Should().Be(strategyName);
        body.Config.StrategyName.Should().Be(strategyName);
        body.Config.Market.Should().Be("BTC-USD");
    }

    [TestMethod]
    public async Task GivenExistingStrategy_WhenUpdate_ThenReturns204AndPersistsChanges()
    {
        var client = GetTestClient();
        var originalName = $"Grid-{Guid.NewGuid():N}";
        var updatedName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, originalName);

        var response = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(updatedName).Replace("\"spacing\": 0.5", "\"spacing\": 1.25")));

        response.AssertStatusCode(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{id}");
        var body = await getResponse.ReadAndAssertSuccessAsync<StrategyDto>();
        body.Name.Should().Be(updatedName);
        body.Config.StrategyName.Should().Be(updatedName);
        body.Config.Grid.Should().NotBeNull();
        body.Config.Grid!.Spacing.Should().Be(1.25m);
        body.Version.Should().BeGreaterThan(1);
    }

    [TestMethod]
    public async Task GivenExistingStrategy_WhenDelete_ThenReturns204AndRemovesStrategyFromReads()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var deleteResponse = await client.DeleteAsync($"{BaseUrl}/{id}");

        deleteResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"{BaseUrl}/{id}");
        getResponse.AssertStatusCode(HttpStatusCode.NotFound);

        var listResponse = await client.GetAsync(BaseUrl);
        var list = await listResponse.ReadAndAssertSuccessAsync<List<StrategySummaryDto>>();
        list.Should().NotContain(strategy => strategy.Id == id);
    }

    [TestMethod]
    public async Task GivenDuplicateStrategyName_WhenCreate_ThenReturns409()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";

        await CreateStrategyAsync(client, strategyName);

        var response = await client.PostAsync(BaseUrl, GetJsonContent(CreateValidGridConfigJson(strategyName)));

        response.AssertStatusCode(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("duplicate_name");
        body.GetProperty("errorMessage").GetString().Should().Contain(strategyName);
    }

    [TestMethod]
    public async Task GivenUnknownId_WhenGetById_ThenReturns404()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenMalformedJson_WhenValidate_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        const string json = "{";

        var response = await client.PostAsync(
            "api/strategies/validate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static StringContent GetJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string CreateValidGridConfigJson(string? strategyName = null)
    {
        return ValidGridConfigJsonTemplate.Replace("__NAME__", strategyName ?? $"Grid-{Guid.NewGuid():N}");
    }

    private static async Task<Guid> CreateStrategyAsync(HttpClient client, string strategyName)
    {
        var response = await client.PostAsync(BaseUrl, GetJsonContent(CreateValidGridConfigJson(strategyName)));
        response.AssertStatusCode(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}