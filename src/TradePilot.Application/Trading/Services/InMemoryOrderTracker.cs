using System.Collections.Concurrent;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Trading.Services;

public sealed class InMemoryOrderTracker : IOrderTracker
{
    private readonly ConcurrentDictionary<string, TrackedOrder> _orders = new();

    public void TrackOrder(string orderId, string gridCycleId, int level, string symbol,
        OrderSide side, decimal price, decimal size, TradeType tradeType)
    {
        if (string.IsNullOrEmpty(orderId)) return;

        _orders[orderId] = new TrackedOrder
        {
            OrderId = orderId,
            GridCycleId = gridCycleId,
            Level = level,
            Symbol = symbol,
            Side = side,
            Price = price,
            Size = size,
            TradeType = tradeType,
        };
    }

    public TrackedOrder? GetOrder(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return order;
    }

    public IReadOnlyList<TrackedOrder> GetOrdersForCycle(string gridCycleId)
    {
        return _orders.Values
            .Where(o => string.Equals(o.GridCycleId, gridCycleId, StringComparison.Ordinal))
            .ToList()
            .AsReadOnly();
    }

    public void RemoveOrder(string orderId)
    {
        _orders.TryRemove(orderId, out _);
    }

    public void Clear()
    {
        _orders.Clear();
    }
}
