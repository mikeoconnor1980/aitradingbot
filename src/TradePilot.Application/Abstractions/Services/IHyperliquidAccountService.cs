using TradePilot.Application.MarketData.Models;

namespace TradePilot.Application.Abstractions.Services;

public interface IHyperliquidAccountService
{
    Task<AccountSummaryDto> GetAccountSummaryAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? walletAddress = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        string? asset = null,
        string? walletAddress = null,
        CancellationToken cancellationToken = default);
}
