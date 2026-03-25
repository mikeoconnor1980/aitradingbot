using TradingApp.Api.Models;

namespace TradingApp.Api.Services;

public interface IHyperliquidAccountService
{
    Task<AccountSummaryDto> GetAccountSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionDto>> GetPositionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(CancellationToken cancellationToken = default);
}