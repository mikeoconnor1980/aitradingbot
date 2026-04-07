using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public interface IStateRecoveryService
{
    Task<GridState> RecoverAsync(
        string strategyName,
        string symbol,
        string walletAddress,
        IOrderTracker orderTracker,
        CancellationToken cancellationToken = default);
}
