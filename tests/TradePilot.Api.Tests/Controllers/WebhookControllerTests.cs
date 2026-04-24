using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Api.Tests.Infrastructure;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;
using TradePilot.Persistence;

namespace TradePilot.Api.Tests.Controllers;

[TestClass]
public sealed class WebhookControllerTests : BaseControllerTests
{
    private const string AgentId = "webhook-agent-1";
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string WalletAddress = "0xb63a3948477254cc17E0fb444050B9E161FCcFA3";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Hyperliquid:PrivateKey", "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e");
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        var store = new AgentCommandStore();
        store.ProcessHeartbeat(new AgentHeartbeat
        {
            AgentId = AgentId,
            State = AgentState.Idle,
            MachineName = "test-machine",
            TimestampUtc = DateTimeOffset.UtcNow,
            WalletAddress = WalletAddress,
        });

        services.RemoveAll<AgentCommandStore>();
        services.AddSingleton(store);
    }

    [TestMethod]
    public async Task GivenBuyWebhook_WhenPosted_ThenQueuesPlaceOrderCommand()
    {
        var client = GetTestClient(authenticate: false);
        var token = await SeedWebhookAsync("BTC alerts", SubscriptionTier.Pro);

        var response = await client.PostAsJsonAsync(
            $"api/webhooks/tradingview/{token}",
            new
            {
                action = "buy",
                ticker = "BTCUSDT",
                contracts = 0.02m,
                orderType = "market"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var commands = store.GetPendingCommands(AgentId);

        commands.Should().ContainSingle();
        var command = commands.Single();
        command.Type.Should().Be(AgentCommandType.PlaceOrder);
        command.OrderPayload.Should().NotBeNull();
        command.OrderPayload!.Asset.Should().Be("BTC");
        command.OrderPayload.Side.Should().Be("buy");
    }

    [TestMethod]
    public async Task GivenCloseWebhook_WhenPosted_ThenQueuesClosePositionCommand()
    {
        var client = GetTestClient(authenticate: false);
        var token = await SeedWebhookAsync("ETH alerts", SubscriptionTier.Pro);

        var response = await client.PostAsJsonAsync(
            $"api/webhooks/tradingview/{token}",
            new
            {
                action = "close",
                ticker = "ETHUSD.P",
                contracts = 1.5m
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        var commands = store.GetPendingCommands(AgentId);

        commands.Should().ContainSingle();
        var command = commands.Single();
        command.Type.Should().Be(AgentCommandType.ClosePosition);
        command.ClosePositionPayload.Should().NotBeNull();
        command.ClosePositionPayload!.Asset.Should().Be("ETH");
        command.ClosePositionPayload.Amount.Should().Be(1.5m);
    }

    [TestMethod]
    public async Task GivenBeginnerWebhook_WhenPosted_ThenReturnsForbidden()
    {
        var client = GetTestClient(authenticate: false);
        var token = await SeedWebhookAsync("BTC alerts", SubscriptionTier.Beginner);

        var response = await client.PostAsJsonAsync(
            $"api/webhooks/tradingview/{token}",
            new
            {
                action = "buy",
                ticker = "BTCUSDT",
                contracts = 0.02m,
                orderType = "market"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = GetCurrentFactory().Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<AgentCommandStore>();
        store.GetPendingCommands(AgentId).Should().BeEmpty();
    }

    private async Task<string> SeedWebhookAsync(string label, SubscriptionTier tier)
    {
        using var scope = GetCurrentFactory().Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradePilotDbContext>();

        if (!db.UserWalletAddresses.Any(x => x.UserId == UserId && x.WalletAddress == WalletAddress))
        {
            db.UserWalletAddresses.Add(UserWalletAddress.Create(UserId, WalletAddress));
        }

        db.Subscriptions.RemoveRange(db.Subscriptions.Where(x => x.UserId == UserId));
        db.Subscriptions.Add(Subscription.Create(UserId, tier, Subscription.TrialDurationDays));

        var webhook = WebhookConfig.Create(UserId, label, null, null);
        db.WebhookConfigs.Add(webhook);
        await db.SaveChangesAsync();

        return webhook.Token;
    }

    private WebApplicationFactory<Program> GetCurrentFactory()
    {
        var factoryField = typeof(BaseControllerTests)
            .GetField("_factory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var factory = (WebApplicationFactory<Program>?)factoryField?.GetValue(this);
        factory.Should().NotBeNull();
        return factory!;
    }
}