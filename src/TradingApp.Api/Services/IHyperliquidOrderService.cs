using TradingApp.Api.Models;

namespace TradingApp.Api.Services;

public interface IHyperliquidOrderService
{
    Task<PlaceOrderResponse> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);

    Task<TestSignResponse> TestSignAsync(CancellationToken cancellationToken = default);
}
