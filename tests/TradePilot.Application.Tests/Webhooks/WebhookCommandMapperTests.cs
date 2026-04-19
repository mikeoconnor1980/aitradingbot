using TradePilot.Application.Agent.Models;
using TradePilot.Application.Webhooks.Models;
using TradePilot.Application.Webhooks.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Webhooks;

[TestClass]
public sealed class WebhookCommandMapperTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [TestMethod]
    public void GivenBuyPayload_WhenMap_ThenReturnsPlaceOrderCommand()
    {
        var webhook = WebhookConfig.Create(UserId, "BTC alerts", "BTC", null);
        var payload = new TradingViewWebhookPayload
        {
            Action = "buy",
            Ticker = "BTCUSDT",
            Contracts = 0.05m,
            OrderType = "market",
            StopLoss = 60000m,
            TakeProfit = 68000m,
        };

        var command = WebhookCommandMapper.Map(payload, webhook, "agent-1");

        command.Type.Should().Be(AgentCommandType.PlaceOrder);
        command.OrderPayload.Should().NotBeNull();
        command.OrderPayload!.Asset.Should().Be("BTC");
        command.OrderPayload.Side.Should().Be("buy");
        command.OrderPayload.Size.Should().Be(0.05m);
        command.OrderPayload.StopLossPrice.Should().Be(60000m);
        command.OrderPayload.TakeProfitPrice.Should().Be(68000m);
    }

    [TestMethod]
    public void GivenClosePayload_WhenMap_ThenReturnsClosePositionCommand()
    {
        var webhook = WebhookConfig.Create(UserId, "ETH alerts", "ETH", null);
        var payload = new TradingViewWebhookPayload
        {
            Action = "close",
            Ticker = "ETHUSD.P",
            Contracts = 1.25m,
        };

        var command = WebhookCommandMapper.Map(payload, webhook, "agent-2");

        command.Type.Should().Be(AgentCommandType.ClosePosition);
        command.ClosePositionPayload.Should().NotBeNull();
        command.ClosePositionPayload!.Asset.Should().Be("ETH");
        command.ClosePositionPayload.Amount.Should().Be(1.25m);
    }
}using TradePilot.Application.Agent.Models;
using TradePilot.Application.Webhooks.Models;
using TradePilot.Application.Webhooks.Services;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Tests.Webhooks;

[TestClass]
public sealed class WebhookCommandMapperTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [TestMethod]
    public void GivenBuyPayload_WhenMap_ThenReturnsPlaceOrderCommand()
    {
        var webhook = WebhookConfig.Create(UserId, "BTC alerts", "BTC", null);
        var payload = new TradingViewWebhookPayload
        {
            Action = "buy",
            Ticker = "BTCUSDT",
            Contracts = 0.05m,
            OrderType = "market",
            StopLoss = 60000m,
            TakeProfit = 68000m,
        };

        var command = WebhookCommandMapper.Map(payload, webhook, "agent-1");

        command.Type.Should().Be(AgentCommandType.PlaceOrder);
        command.OrderPayload.Should().NotBeNull();
        command.OrderPayload!.Asset.Should().Be("BTC");
        command.OrderPayload.Side.Should().Be("buy");
        command.OrderPayload.Size.Should().Be(0.05m);
        command.OrderPayload.StopLossPrice.Should().Be(60000m);
        command.OrderPayload.TakeProfitPrice.Should().Be(68000m);
    }

    [TestMethod]
    public void GivenClosePayload_WhenMap_ThenReturnsClosePositionCommand()
    {
        var webhook = WebhookConfig.Create(UserId, "ETH alerts", "ETH", null);
        var payload = new TradingViewWebhookPayload
        {
            Action = "close",
            Ticker = "ETHUSD.P",
            Contracts = 1.25m,
        };

        var command = WebhookCommandMapper.Map(payload, webhook, "agent-2");

        command.Type.Should().Be(AgentCommandType.ClosePosition);
        command.ClosePositionPayload.Should().NotBeNull();
        command.ClosePositionPayload!.Asset.Should().Be("ETH");
        command.ClosePositionPayload.Amount.Should().Be(1.25m);
    }
}