using TradingApp.Application.MarketData.Models;

namespace TradingApp.Application.Abstractions.Services;

public interface IHyperliquidAccountService
{
    Task<AccountSummaryDto> GetAccountSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        string? asset = null,
        CancellationToken cancellationToken = default);
}
