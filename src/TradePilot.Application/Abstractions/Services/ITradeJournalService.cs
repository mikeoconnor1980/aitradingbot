using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>Projects persisted exchange fills into a durable logical trade lifecycle.</summary>
public interface ITradeJournalService
{
    Task RecordFillAsync(
        LiveFill fill,
        TradeExecutionEvidence? evidence,
        bool isExit,
        TradeExitReason? exitReason = null,
        CancellationToken cancellationToken = default);
}
