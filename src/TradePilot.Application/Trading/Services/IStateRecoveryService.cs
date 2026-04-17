using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Trading.Services;

public interface IStateRecoveryService
{
    Task<GridState> RecoverAsync(
        string strategyName,
        string symbol,
        string walletAddress,
        IOrderTracker orderTracker,
        CancellationToken cancellationToken = default);
}
