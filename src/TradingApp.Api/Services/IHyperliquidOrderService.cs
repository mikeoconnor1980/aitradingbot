using TradingApp.Api.Models;

namespace TradingApp.Api.Services;

public interface IHyperliquidOrderService
{
    Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);

    Task CancelOrderAsync(string orderId, string asset, CancellationToken cancellationToken = default);

    Task CancelAllOrdersAsync(string asset, CancellationToken cancellationToken = default);

    Task ModifyOrderAsync(
        string orderId,
        string asset,
        string side,
        decimal price,
        decimal size,
        CancellationToken cancellationToken = default);

    Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default);

    Task UpdateLeverageAsync(string asset, int leverage, bool isCross = true, CancellationToken cancellationToken = default);
}
