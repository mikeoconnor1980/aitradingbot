using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Abstractions.Services;

/// <summary>
/// Extended execution engine capability for querying live position state.
/// Implemented by LiveExecutionEngine to provide exchange-authoritative position data.
/// </summary>
public interface IPositionQueryable
{
    Task<PositionState> QueryPositionAsync(string symbol, CancellationToken cancellationToken = default);

    Task<decimal> QueryAccountEquityAsync(CancellationToken cancellationToken = default);
}
