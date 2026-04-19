using System.Net.Http.Headers;
using System.Net.Http.Json;
using TradePilot.Api.Models;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class WebhookManagementControllerTests : BaseControllerTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    [TestMethod]
    public async Task GivenBeginnerSubscription_WhenGetWebhooks_ThenReturnsForbidden()
    {
        await SeedSubscriptionAsync(SubscriptionTier.Beginner);
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("api/webhooks");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task GivenProSubscription_WhenCreateWebhook_ThenReturnsCreated()
    {
        await SeedSubscriptionAsync(SubscriptionTier.Pro);
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/webhooks", new CreateWebhookRequest
        {
            Label = "BTC alerts",
            DefaultAsset = "BTC",
            TargetAgentId = null,
        });

        var result = await response.ReadAndAssertCreatedAsync<WebhookConfigDto>();
        result.Label.Should().Be("BTC alerts");
        result.DefaultAsset.Should().Be("BTC");
        result.Token.Should().NotBeNullOrWhiteSpace();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateTestToken(userId: UserId.ToString(), email: "webhooks@test.dev", displayName: "Webhook Tester"));
        return client;
    }

    private async Task SeedSubscriptionAsync(SubscriptionTier tier)
    {
        using var db = CreateTestDbContext();
        db.Subscriptions.RemoveRange(db.Subscriptions.Where(x => x.UserId == UserId));
        db.Subscriptions.Add(Subscription.Create(UserId, tier, Subscription.TrialDurationDays));
        await db.SaveChangesAsync();
    }
}