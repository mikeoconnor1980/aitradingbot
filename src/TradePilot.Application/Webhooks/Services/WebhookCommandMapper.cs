using TradePilot.Application.Agent.Models;
using TradePilot.Application.Webhooks.Models;
using TradePilot.Domain.Entities;

namespace TradePilot.Application.Webhooks.Services;

public static class WebhookCommandMapper
{
    public static AgentCommand Map(TradingViewWebhookPayload payload, WebhookConfig webhookConfig, string agentId)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(webhookConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var action = payload.Action.Trim().ToLowerInvariant();
        var asset = SymbolMapper.ResolveAsset(payload.Ticker, webhookConfig.DefaultAsset);

        return action switch
        {
            "buy" or "sell" => BuildPlaceOrderCommand(payload, agentId, asset, action),
            "close" => BuildClosePositionCommand(payload, agentId, asset),
            _ => throw new ArgumentException($"Unsupported TradingView action '{payload.Action}'.", nameof(payload))
        };
    }

    private static AgentCommand BuildPlaceOrderCommand(
        TradingViewWebhookPayload payload,
        string agentId,
        string asset,
        string action)
    {
        if (!payload.Contracts.HasValue || payload.Contracts.Value <= 0m)
        {
            throw new ArgumentException("Contracts must be provided and positive for buy/sell actions.", nameof(payload));
        }

        var orderType = string.IsNullOrWhiteSpace(payload.OrderType)
            ? (payload.Price.HasValue ? "limit" : "market")
            : payload.OrderType.Trim().ToLowerInvariant();

        if (orderType == "limit" && !payload.Price.HasValue)
        {
            throw new ArgumentException("Price is required for limit orders.", nameof(payload));
        }

        return new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.PlaceOrder,
            OrderPayload = new OrderCommandPayload
            {
                Asset = asset,
                Side = action,
                OrderType = orderType,
                Price = orderType == "limit" ? payload.Price : null,
                Size = payload.Contracts.Value,
                StopLossPrice = payload.StopLoss,
                TakeProfitPrice = payload.TakeProfit,
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static AgentCommand BuildClosePositionCommand(
        TradingViewWebhookPayload payload,
        string agentId,
        string asset)
    {
        if (payload.Contracts is <= 0m)
        {
            throw new ArgumentException("Contracts must be positive when provided for close actions.", nameof(payload));
        }

        return new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.ClosePosition,
            ClosePositionPayload = new ClosePositionPayload
            {
                Asset = asset,
                Amount = payload.Contracts,
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}