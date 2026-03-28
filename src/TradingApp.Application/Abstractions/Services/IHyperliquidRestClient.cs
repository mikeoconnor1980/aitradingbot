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
        long? endTime = null,
        CancellationToken cancellationToken = default);

    Task<List<CandleSnapshotDto>> GetCandleSnapshotsAsync(
        string asset,
        string timeframe,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default);

    Task<List<FillEventDto>> GetUserFillsAsync(
        string walletAddress,
        long? startTimeMs = null,
        CancellationToken cancellationToken = default);
}
