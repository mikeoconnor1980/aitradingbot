using TradePilot.Application.Analyst.Models;

namespace TradePilot.Application.Abstractions.Services;

/// <summary>Answers trading questions by orchestrating allow-listed read-only TradePilot capabilities.</summary>
public interface ITradingAnalyst
{
    /// <summary>Analyses a natural-language question using request-scoped TradePilot facts.</summary>
    Task<TradingAnalystResult> AnalyseAsync(
        TradingAnalystRequest request,
        CancellationToken cancellationToken);
}
