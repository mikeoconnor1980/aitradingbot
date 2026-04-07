namespace TradingApp.Application.Trading.Services;

using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Enums;

public interface IOrderTracker
{
    void TrackOrder(string orderId, string gridCycleId, int level, string symbol,
        OrderSide side, decimal price, decimal size, TradeType tradeType);

    TrackedOrder? GetOrder(string orderId);

    IReadOnlyList<TrackedOrder> GetOrdersForCycle(string gridCycleId);

    void RemoveOrder(string orderId);

    void Clear();
}
