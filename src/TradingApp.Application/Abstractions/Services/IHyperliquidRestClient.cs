using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidRestClient
{
    Task<bool> CheckConnectivityAsync(CancellationToken cancellationToken = default);

    Task<TResponse> PostExchangeAsync<TResponse>(
        object signedPayload,
        CancellationToken cancellationToken = default);

    Task<TResponse> PostInfoAsync<TResponse>(
        object request,
        CancellationToken cancellationToken = default);

    Task<MarketInfoDto?> GetMarketInfoAsync(
        string asset,
        CancellationToken cancellationToken = default);

    Task<List<CandleDto>> GetCandlesAsync(
        string asset,
        string timeframe,
        CancellationToken cancellationToken = default);
}
