using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Trading;
using TradingApp.Persistence;

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
        var id = body.GetProperty("id").GetGuid();
        id.Should().NotBeEmpty();

        var getResponse = await client.GetAsync($"{BaseUrl}/{id}");
        var strategy = await getResponse.ReadAndAssertSuccessAsync<StrategyDto>();
        strategy.Version.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenValidStrategyConfig_WhenCreate_ThenPersistsInitialRevision()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";

        var strategyId = await CreateStrategyAsync(client, strategyName);
        var revisions = await GetStrategyRevisionsAsync(strategyId);

        revisions.Should().HaveCount(1);
        revisions[0].RevisionNumber.Should().Be(1);
        revisions[0].Source.Should().Be(RevisionSource.Ui);
        revisions[0].ChangeSummary.Should().Be("Initial version");

        var config = JsonSerializer.Deserialize<StrategyConfig>(revisions[0].ConfigJson, BaseControllerTestsJson.Options);
        config.Should().NotBeNull();
        config!.StrategyName.Should().Be(strategyName);
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
    public async Task GivenCreatedStrategy_WhenGetVersions_ThenReturnsOneRevision()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{id}/versions?page=1&pageSize=20");

        var body = await response.ReadAndAssertSuccessAsync<PagedResult<StrategyRevisionSummaryDto>>();
        body.TotalCount.Should().Be(1);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Should().HaveCount(1);
        body.Items[0].RevisionNumber.Should().Be(1);
        body.Items[0].ChangeSummary.Should().Be("Initial version");
        body.Items[0].Source.Should().Be(nameof(RevisionSource.Ui));
    }

    [TestMethod]
    public async Task GivenUpdatedStrategy_WhenGetVersions_ThenReturnsTwoRevisions()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var updateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 1.25")));

        updateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"{BaseUrl}/{id}/versions?page=1&pageSize=20");

        var body = await response.ReadAndAssertSuccessAsync<PagedResult<StrategyRevisionSummaryDto>>();
        body.TotalCount.Should().Be(2);
        body.Items.Should().HaveCount(2);
        body.Items.Select(item => item.RevisionNumber).Should().Equal(2, 1);
        body.Items[0].ChangeSummary.Should().Contain("grid.spacing: 0.5 → 1.25");
    }

    [TestMethod]
    public async Task GivenMultipleRevisions_WhenGetVersionsWithPagination_ThenReturnsRequestedPage()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var firstUpdateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 1.25")));
        firstUpdateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var secondUpdateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 1.5")));
        secondUpdateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"{BaseUrl}/{id}/versions?page=2&pageSize=1");

        var body = await response.ReadAndAssertSuccessAsync<PagedResult<StrategyRevisionSummaryDto>>();
        body.TotalCount.Should().Be(3);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(1);
        body.Items.Should().HaveCount(1);
        body.Items[0].RevisionNumber.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenExistingRevision_WhenGetVersion_ThenReturnsFullSnapshot()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var updateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 1.25")));

        updateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"{BaseUrl}/{id}/versions/2");

        var body = await response.ReadAndAssertSuccessAsync<StrategyRevisionDto>();
        body.RevisionNumber.Should().Be(2);
        body.Source.Should().Be(nameof(RevisionSource.Ui));
        body.ChangeSummary.Should().Contain("grid.spacing: 0.5 → 1.25");
        body.Config.StrategyName.Should().Be(strategyName);
        body.Config.Grid.Should().NotBeNull();
        body.Config.Grid!.Spacing.Should().Be(1.25m);
    }

    [TestMethod]
    public async Task GivenTwoRevisions_WhenGetDiff_ThenReturnsFieldChanges()
    {
        var client = GetTestClient();
        var strategyName = $"Diff-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var updateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(
                CreateValidGridConfigJson(strategyName)
                    .Replace("\"spacing\": 0.5", "\"spacing\": 0.8")
                    .Replace("\"value\": 2.0", "\"value\": 3.0")));

        updateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var response = await client.GetAsync($"{BaseUrl}/{id}/diff?from=1&to=2");

        var body = await response.ReadAndAssertSuccessAsync<StrategyDiffDto>();
        body.FromRevision.Should().Be(1);
        body.ToRevision.Should().Be(2);
        body.Changes.Should().Contain(change =>
            change.Path == "grid.spacing" &&
            change.OldValue == "0.5" &&
            change.NewValue == "0.8");
        body.Changes.Should().Contain(change =>
            change.Path == "exit.takeProfit.value" &&
            change.OldValue == "2.0" &&
            change.NewValue == "3.0");
    }

    [TestMethod]
    public async Task GivenSameRevision_WhenGetDiff_ThenReturns400()
    {
        var client = GetTestClient();
        var strategyName = $"Same-Diff-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{id}/diff?from=1&to=1");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenNonExistentRevision_WhenGetDiff_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyName = $"Missing-Diff-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{id}/diff?from=1&to=99");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenPausedStrategy_WhenRestoreRevision_ThenReturns204AndCreatesRestoreRevision()
    {
        var client = GetTestClient();
        var strategyName = $"Restore-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var updateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 0.8")));

        updateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var restoreResponse = await client.PostAsync($"{BaseUrl}/{id}/versions/1/restore", null);

        restoreResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var versionsResponse = await client.GetAsync($"{BaseUrl}/{id}/versions?page=1&pageSize=20");
        var versions = await versionsResponse.ReadAndAssertSuccessAsync<PagedResult<StrategyRevisionSummaryDto>>();

        versions.TotalCount.Should().Be(3);
        versions.Items[0].RevisionNumber.Should().Be(3);
        versions.Items[0].Source.Should().Be(nameof(RevisionSource.Restore));
        versions.Items[0].Label.Should().Be("Restored from revision 1");

        var strategyResponse = await client.GetAsync($"{BaseUrl}/{id}");
        var strategy = await strategyResponse.ReadAndAssertSuccessAsync<StrategyDto>();
        strategy.Version.Should().Be(3);
        strategy.Config.Grid.Should().NotBeNull();
        strategy.Config.Grid!.Spacing.Should().Be(0.5m);
    }

    [TestMethod]
    public async Task GivenNonExistentRevision_WhenRestore_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyName = $"Missing-Restore-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.PostAsync($"{BaseUrl}/{id}/versions/99/restore", null);

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenRunningStrategy_WhenRestore_ThenReturns409Conflict()
    {
        var client = GetTestClient();
        var strategyName = $"Running-Restore-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        await SetStrategyRunningStateAsync(id, true);

        var response = await client.PostAsync($"{BaseUrl}/{id}/versions/1/restore", null);

        response.AssertStatusCode(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("conflict");
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
        body.Version.Should().Be(2);
    }

    [TestMethod]
    public async Task GivenExistingStrategy_WhenUpdated_ThenCreatesNewRevisionWithChangeSummary()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var updateResponse = await client.PutAsync(
            $"{BaseUrl}/{id}",
            GetJsonContent(CreateValidGridConfigJson(strategyName).Replace("\"spacing\": 0.5", "\"spacing\": 1.25")));

        updateResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var revisions = await GetStrategyRevisionsAsync(id);

        revisions.Should().HaveCount(2);
        revisions.Select(revision => revision.RevisionNumber).Should().Equal(1, 2);

        var latestRevision = revisions[1];
        latestRevision.Source.Should().Be(RevisionSource.Ui);
        latestRevision.ChangeSummary.Should().Contain("grid.spacing: 0.5 → 1.25");

        var config = JsonSerializer.Deserialize<StrategyConfig>(latestRevision.ConfigJson, BaseControllerTestsJson.Options);
        config.Should().NotBeNull();
        config!.Grid.Should().NotBeNull();
        config.Grid!.Spacing.Should().Be(1.25m);
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
    public async Task GivenUnknownStrategy_WhenGetVersions_ThenReturns404()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/versions?page=1&pageSize=20");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenStrategyWithBacktests_WhenGetBacktestsByStrategy_ThenReturnsPagedResults()
    {
        var client = GetTestClient();
        var strategyName = $"Backtest-Grid-{Guid.NewGuid():N}";
        var strategyId = await CreateStrategyAsync(client, strategyName);

        await AddBacktestRunAsync(strategyId, 2, totalTrades: 14, totalPnl: 320m);
        await AddBacktestRunAsync(Guid.NewGuid(), null, totalTrades: 8, totalPnl: 120m);

        var response = await client.GetAsync($"{BaseUrl}/{strategyId}/backtests?page=1&pageSize=20");

        var result = await response.ReadAndAssertSuccessAsync<PagedResult<BacktestSummaryDto>>();

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items[0].StrategyId.Should().Be(strategyId);
        result.Items[0].StrategyRevisionId.Should().Be(2);
        result.Items[0].StrategyName.Should().Be(strategyName);
        result.Items[0].TotalTrades.Should().Be(14);
        result.Items[0].TotalPnl.Should().Be(320m);
    }

    [TestMethod]
    public async Task GivenNonExistentStrategy_WhenGetBacktestsByStrategy_ThenReturnsNotFound()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}/backtests?page=1&pageSize=20");

        response.AssertStatusCode(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GivenStrategyWithNoBacktests_WhenGetBacktestsByStrategy_ThenReturnsEmptyPagedResult()
    {
        var client = GetTestClient();
        var strategyName = $"Empty-Grid-{Guid.NewGuid():N}";
        var strategyId = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{strategyId}/backtests?page=1&pageSize=20");

        var result = await response.ReadAndAssertSuccessAsync<PagedResult<BacktestSummaryDto>>();

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [DataTestMethod]
    [DataRow(0, 20, "page must be greater than or equal to 1")]
    [DataRow(1, 0, "pageSize must be between 1 and 100")]
    [DataRow(1, 101, "pageSize must be between 1 and 100")]
    public async Task GivenInvalidPaging_WhenGetBacktestsByStrategy_ThenReturnsBadRequest(int page, int pageSize, string errorMessage)
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var strategyId = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{strategyId}/backtests?page={page}&pageSize={pageSize}");

        response.AssertStatusCode(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain(errorMessage);
    }

    [TestMethod]
    public async Task GivenUnknownRevision_WhenGetVersion_ThenReturns404()
    {
        var client = GetTestClient();
        var strategyName = $"Grid-{Guid.NewGuid():N}";
        var id = await CreateStrategyAsync(client, strategyName);

        var response = await client.GetAsync($"{BaseUrl}/{id}/versions/99");

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

    private async Task<List<StrategyRevision>> GetStrategyRevisionsAsync(Guid strategyId)
    {
        var options = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        await using var context = new TradingAppDbContext(options);

        return await context.StrategyRevisions
            .AsNoTracking()
            .Where(revision => revision.StrategyId == strategyId)
            .OrderBy(revision => revision.RevisionNumber)
            .ToListAsync();
    }

    private async Task SetStrategyRunningStateAsync(Guid strategyId, bool isRunning)
    {
        var options = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        await using var context = new TradingAppDbContext(options);
        var strategy = await context.Strategies.FirstAsync(item => item.Id == strategyId);
        strategy.SetRunningState(isRunning);
        await context.SaveChangesAsync();
    }

    private async Task AddBacktestRunAsync(Guid strategyId, int? strategyRevisionId, int totalTrades, decimal totalPnl)
    {
        var options = new DbContextOptionsBuilder<TradingAppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        await using var context = new TradingAppDbContext(options);
        var run = BacktestRun.CreateQueued(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1706745599000,
            strategyConfigJson: JsonSerializer.Serialize(new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Grid,
                StrategyName = "Strategy Backtest",
                Exchange = "Hyperliquid",
                Market = "BTC",
                Timeframe = "15m",
                Direction = Direction.Long,
                Enabled = true,
                Grid = new GridConfig
                {
                    Levels = 10,
                    EntryMode = EntryModes.AutoFromSignalCandle,
                    Spacing = 0.5m,
                    BreakdownThreshold = -3m,
                },
                Exit = new ExitConfig
                {
                    TakeProfit = new ExitRuleConfig
                    {
                        Enabled = true,
                        Type = ExitRuleType.FixedPercent,
                        Value = 1m,
                    },
                    StopLoss = new ExitRuleConfig
                    {
                        Enabled = true,
                        Type = ExitRuleType.FixedPercent,
                        Value = 5m,
                    },
                },
                Risk = new RiskConfig
                {
                    PositionSizeType = PositionSizeType.FixedNotional,
                    PositionSizeValue = 100m,
                    Leverage = 3m,
                    MaxOpenTrades = 1,
                    CooldownValue = 0,
                    CooldownUnit = CooldownUnit.Candles,
                },
            }, StrategyJsonOptions.Default),
            executionConfigJson: JsonSerializer.Serialize(new ExecutionConfig
            {
                FeeModel = new FeeModel
                {
                    MakerFeeRate = 0.0001m,
                    TakerFeeRate = 0.00035m,
                    SlippageRate = 0m,
                },
            }, StrategyJsonOptions.Default),
            initialCapital: 10000m,
            strategyId: strategyId,
            strategyRevisionId: strategyRevisionId);

        run.MarkRunning(100);
        run.MarkCompleted(
            candlesReplayed: 100,
            elapsedMs: 500,
            totalTrades: totalTrades,
            winningTrades: Math.Max(1, totalTrades - 4),
            losingTrades: Math.Min(4, totalTrades),
            winRate: 71.4m,
            totalPnl: totalPnl,
            maxDrawdown: -120m,
            averageTradePnl: totalTrades == 0 ? 0m : totalPnl / totalTrades,
            averageHoldTimeMinutes: 45,
            hedgesOpened: 1,
            totalFeesPaid: 12m,
            tradesJson: "[]",
            equityTimeSeriesJson: "[]");

        await context.BacktestRuns.AddAsync(run);
        await context.SaveChangesAsync();
    }
}