using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Domain.Trading;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Controllers;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
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
    public async Task GivenSavedStrategy_WhenPromotedToTemplate_ThenReturnsCreatedAndTemplateAppearsInLibrary()
    {
        var client = GetTestClient();
        await SeedStrategyTemplateAsync(
            slug: $"allowed-tags-{Guid.NewGuid():N}",
            name: $"Allowed Tags {Guid.NewGuid():N}",
            tags: ["trend", "ema", "range"]);

        var strategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");
        var request = new PromoteStrategyTemplateRequest
        {
            Name = $"Library Promotion {Guid.NewGuid():N}",
            Description = "Promoted from a saved user strategy.",
            Tags = ["trend", "ema"],
        };

        var response = await client.PostAsync(
            $"{BaseUrl}/{strategyId}/promote-template",
            GetStringContent(request));

        response.AssertStatusCode(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = body.GetProperty("id").GetGuid();

        var templatesResponse = await client.GetAsync($"{BaseUrl}/templates");
        var templates = await templatesResponse.ReadAndAssertSuccessAsync<List<StrategyTemplateDto>>();
        var promotedTemplate = templates.Single(template => template.Id == templateId);

        promotedTemplate.Name.Should().Be(request.Name);
        promotedTemplate.Description.Should().Be(request.Description);
        promotedTemplate.Tags.Should().Equal("trend", "ema");
        promotedTemplate.Config.StrategyName.Should().Be(request.Name);
        promotedTemplate.Config.TemplateId.Should().Be(promotedTemplate.Slug);
    }

    [TestMethod]
    public async Task GivenDuplicateTemplateName_WhenPromotedToTemplate_ThenReturns409()
    {
        var client = GetTestClient();
        await SeedStrategyTemplateAsync(
            slug: "trend-pullback-ema-long",
            name: "Trend Pullback EMA Long",
            tags: ["trend", "ema"]);

        var strategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");
        var request = new PromoteStrategyTemplateRequest
        {
            Name = "Trend Pullback EMA Long",
            Description = "Should fail because the name already exists.",
            Tags = ["trend"],
        };

        var response = await client.PostAsync(
            $"{BaseUrl}/{strategyId}/promote-template",
            GetStringContent(request));

        response.AssertStatusCode(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("duplicate_template_name");
        body.GetProperty("errorMessage").GetString().Should().Contain(request.Name);
    }

    [TestMethod]
    public async Task GivenNamesThatNormalizeToSameSlug_WhenPromotedToTemplate_ThenSecondPromotionGetsUniqueSlug()
    {
        var client = GetTestClient();
        await SeedStrategyTemplateAsync(
            slug: $"allowed-range-{Guid.NewGuid():N}",
            name: $"Allowed Range {Guid.NewGuid():N}",
            tags: ["range"]);

        var firstStrategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");
        var secondStrategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");

        var firstRequest = new PromoteStrategyTemplateRequest
        {
            Name = "Range Shift Alpha",
            Description = "First promoted template.",
            Tags = ["range"],
        };

        var secondRequest = new PromoteStrategyTemplateRequest
        {
            Name = "Range-Shift Alpha",
            Description = "Second promoted template.",
            Tags = ["range"],
        };

        var firstResponse = await client.PostAsync(
            $"{BaseUrl}/{firstStrategyId}/promote-template",
            GetStringContent(firstRequest));
        firstResponse.AssertStatusCode(HttpStatusCode.Created);

        var secondResponse = await client.PostAsync(
            $"{BaseUrl}/{secondStrategyId}/promote-template",
            GetStringContent(secondRequest));
        secondResponse.AssertStatusCode(HttpStatusCode.Created);

        await using var context = CreateTestDbContext();
        var promotedTemplates = await context.StrategyTemplates
            .AsNoTracking()
            .Where(template => template.Name == firstRequest.Name || template.Name == secondRequest.Name)
            .OrderBy(template => template.Name)
            .ToListAsync();

        promotedTemplates.Should().HaveCount(2);
        promotedTemplates.Select(template => template.Slug).Should().OnlyHaveUniqueItems();
        promotedTemplates.Select(template => template.Slug).Should().OnlyContain(slug => slug.StartsWith("range-shift-alpha", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenPromotedTemplate_WhenUnpublished_ThenReturns204AndRemovesItFromLibrary()
    {
        await SeedAdminGrantAsync();
        var client = GetTestClient();
        await SeedStrategyTemplateAsync(
            slug: $"allowed-tags-{Guid.NewGuid():N}",
            name: $"Allowed Tags {Guid.NewGuid():N}",
            tags: ["trend"]);

        var strategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");
        var promoteRequest = new PromoteStrategyTemplateRequest
        {
            Name = $"Unpublish Target {Guid.NewGuid():N}",
            Description = "Template to remove from the library.",
            Tags = ["trend"],
        };

        var promoteResponse = await client.PostAsync(
            $"{BaseUrl}/{strategyId}/promote-template",
            GetStringContent(promoteRequest));
        promoteResponse.AssertStatusCode(HttpStatusCode.Created);

        var createdBody = await promoteResponse.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createdBody.GetProperty("id").GetGuid();

        var unpublishResponse = await client.DeleteAsync($"{BaseUrl}/templates/{templateId}");
        unpublishResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var templatesResponse = await client.GetAsync($"{BaseUrl}/templates");
        var templates = await templatesResponse.ReadAndAssertSuccessAsync<List<StrategyTemplateDto>>();
        templates.Should().NotContain(template => template.Id == templateId);

        await using var context = CreateTestDbContext();
        var unpublishedTemplate = await context.StrategyTemplates
            .AsNoTracking()
            .SingleAsync(template => template.Id == templateId);
        unpublishedTemplate.IsActive.Should().BeFalse();
        unpublishedTemplate.IsSystemTemplate.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenPromotedTemplate_WhenRenamedByAdmin_ThenReturns204AndUpdatesTemplate()
    {
        await SeedAdminGrantAsync();
        var client = GetTestClient();
        await SeedStrategyTemplateAsync(
            slug: $"allowed-tags-{Guid.NewGuid():N}",
            name: $"Allowed Tags {Guid.NewGuid():N}",
            tags: ["trend"]);

        var strategyId = await CreateStrategyAsync(client, $"Promote-{Guid.NewGuid():N}");
        var promoteRequest = new PromoteStrategyTemplateRequest
        {
            Name = $"Rename Target {Guid.NewGuid():N}",
            Description = "Template before rename.",
            Tags = ["trend"],
        };

        var promoteResponse = await client.PostAsync(
            $"{BaseUrl}/{strategyId}/promote-template",
            GetStringContent(promoteRequest));
        promoteResponse.AssertStatusCode(HttpStatusCode.Created);

        var createdBody = await promoteResponse.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = createdBody.GetProperty("id").GetGuid();

        await using var context = CreateTestDbContext();
        var originalTemplate = await context.StrategyTemplates
            .AsNoTracking()
            .SingleAsync(template => template.Id == templateId);
        var originalSlug = originalTemplate.Slug;

        var renameRequest = new RenameStrategyTemplateRequest
        {
            Name = $"Renamed Template {Guid.NewGuid():N}",
            Description = "Updated library description."
        };

        var renameResponse = await client.PatchAsync(
            $"{BaseUrl}/templates/{templateId}",
            GetStringContent(renameRequest));

        renameResponse.AssertStatusCode(HttpStatusCode.NoContent);

        var renamedTemplate = await context.StrategyTemplates
            .AsNoTracking()
            .SingleAsync(template => template.Id == templateId);

        renamedTemplate.Name.Should().Be(renameRequest.Name);
        renamedTemplate.Description.Should().Be(renameRequest.Description);
        renamedTemplate.Slug.Should().Be(originalSlug);
    }

    [TestMethod]
    public async Task GivenDuplicateTemplateName_WhenRenamed_ThenReturns409()
    {
        await SeedAdminGrantAsync();
        var client = GetTestClient();
        var existingTemplateId = await SeedStrategyTemplateAsync(
            slug: $"existing-template-{Guid.NewGuid():N}",
            name: $"Existing Template {Guid.NewGuid():N}",
            tags: ["trend"]);
        var renameTargetId = await SeedStrategyTemplateAsync(
            slug: $"rename-target-{Guid.NewGuid():N}",
            name: $"Rename Target {Guid.NewGuid():N}",
            tags: ["trend"]);

        await using var context = CreateTestDbContext();
        var existingTemplate = await context.StrategyTemplates.AsNoTracking().SingleAsync(template => template.Id == existingTemplateId);

        var response = await client.PatchAsync(
            $"{BaseUrl}/templates/{renameTargetId}",
            GetStringContent(new RenameStrategyTemplateRequest
            {
                Name = existingTemplate.Name,
                Description = "Updated description"
            }));

        response.AssertStatusCode(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("duplicate_template_name");
    }

    [TestMethod]
    public async Task GivenSystemTemplate_WhenUnpublished_ThenReturns409()
    {
        await SeedAdminGrantAsync();
        var client = GetTestClient();
        var systemTemplateId = await SeedStrategyTemplateAsync(
            slug: $"system-template-{Guid.NewGuid():N}",
            name: $"System Template {Guid.NewGuid():N}",
            tags: ["starter"],
            isSystemTemplate: true);

        var response = await client.DeleteAsync($"{BaseUrl}/templates/{systemTemplateId}");

        response.AssertStatusCode(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("conflict");
    }

    [TestMethod]
    public async Task GivenNonAdmin_WhenTemplateRenamed_ThenReturns403()
    {
        var client = GetTestClient();
        var templateId = await SeedStrategyTemplateAsync(
            slug: $"rename-target-{Guid.NewGuid():N}",
            name: $"Rename Target {Guid.NewGuid():N}",
            tags: ["trend"]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateTestToken(email: "non-admin@tradepilot.dev", displayName: "Non Admin User"));

        var response = await client.PatchAsync(
            $"{BaseUrl}/templates/{templateId}",
            GetStringContent(new RenameStrategyTemplateRequest
            {
                Name = $"Blocked Rename {Guid.NewGuid():N}",
                Description = "Blocked description"
            }));

        response.AssertStatusCode(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task GivenNonAdmin_WhenTemplateUnpublished_ThenReturns403()
    {
        var client = GetTestClient();
        var templateId = await SeedStrategyTemplateAsync(
            slug: $"remove-target-{Guid.NewGuid():N}",
            name: $"Remove Target {Guid.NewGuid():N}",
            tags: ["trend"]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateTestToken(email: "non-admin@tradepilot.dev", displayName: "Non Admin User"));

        var response = await client.DeleteAsync($"{BaseUrl}/templates/{templateId}");

        response.AssertStatusCode(HttpStatusCode.Forbidden);
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
        await using var context = CreateTestDbContext();

        return await context.StrategyRevisions
            .AsNoTracking()
            .Where(revision => revision.StrategyId == strategyId)
            .OrderBy(revision => revision.RevisionNumber)
            .ToListAsync();
    }

    private async Task SetStrategyRunningStateAsync(Guid strategyId, bool isRunning)
    {
        await using var context = CreateTestDbContext();
        var strategy = await context.Strategies.FirstAsync(item => item.Id == strategyId);
        strategy.SetRunningState(isRunning);
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedStrategyTemplateAsync(string slug, string name, string[] tags, bool isSystemTemplate = false)
    {
        await using var context = CreateTestDbContext();

        var config = new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = name,
            Exchange = "Hyperliquid",
            Market = "BTC-USD",
            Timeframe = "15m",
            Direction = Direction.Long,
            Enabled = true,
            TemplateId = slug,
            Grid = new GridConfig
            {
                Levels = 10,
                Spacing = 0.5m,
                EntryMode = "auto_from_signal_candle",
                BreakdownThreshold = 1.5m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 2.0m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 6.0m,
                },
                ExitOnOppositeSignal = false,
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.PercentWallet,
                PositionSizeValue = 5.0m,
                Leverage = 1.0m,
                MaxOpenTrades = 1,
                CooldownValue = 0,
                CooldownUnit = CooldownUnit.Candles,
                AllowSameCandleReentry = false,
            },
            Metadata = new StrategyMetadata
            {
                Tags = tags,
                Notes = string.Empty,
            },
        };

        var template = StrategyTemplate.Create(
            slug,
            name,
            $"Seeded template for {name}",
            "grid",
            "long",
            "BTC-USD",
            JsonSerializer.Serialize(tags),
            JsonSerializer.Serialize(config, StrategyJsonOptions.Default),
            1,
            isSystemTemplate);

        context.StrategyTemplates.Add(template);
        await context.SaveChangesAsync();

        return template.Id;
    }

    private async Task AddBacktestRunAsync(Guid strategyId, int? strategyRevisionId, int totalTrades, decimal totalPnl)
    {
        await using var context = CreateTestDbContext();
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
            equityTimeSeriesJson: "[]",
            expectancy: 0.56m,
            profitFactor: 2.17m,
            sqn: 1.41m);

        await context.BacktestRuns.AddAsync(run);
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedAdminGrantAsync(string email = "test@tradepilot.dev")
    {
        await using var context = CreateTestDbContext();

        var normalizedEmail = AdminUserGrant.NormalizeEmail(email);
        var existingGrant = await context.AdminUserGrants
            .SingleOrDefaultAsync(grant => grant.Email == normalizedEmail);

        if (existingGrant is not null)
        {
            return existingGrant.Id;
        }

        var grant = AdminUserGrant.Create(normalizedEmail);
        context.AdminUserGrants.Add(grant);
        await context.SaveChangesAsync();

        return grant.Id;
    }
}