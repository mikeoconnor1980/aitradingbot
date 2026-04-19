using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeAccountClient
{
    Exchange Exchange { get; }

    Task<AccountSummaryDto> GetAccountSummaryAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        TradingPair? pair = null,
        string? walletAddress = null,
        CancellationToken cancellationToken = default);
}