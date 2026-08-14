using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.TradeJournal.Services;

/// <summary>Deterministically projects the existing live-fill stream into logical position lifecycles.</summary>
public sealed class TradeJournalService : ITradeJournalService
{
    private readonly ITradeJournalRepository _repository;
    private readonly ICandleRepository _candleRepository;
    private readonly ILogger<TradeJournalService> _logger;

    public TradeJournalService(
        ITradeJournalRepository repository,
        ICandleRepository candleRepository,
        ILogger<TradeJournalService> logger)
    {
        _repository = repository;
        _candleRepository = candleRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordFillAsync(
        LiveFill fill,
        TradeExecutionEvidence? evidence,
        bool isExit,
        TradeExitReason? exitReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fill);
        if (string.IsNullOrWhiteSpace(fill.UserId))
        {
            _logger.LogWarning("Trade journal projection skipped because the fill has no user identity");
            return;
        }

        var openTrade = await _repository.GetOpenAsync(
            fill.UserId,
            fill.Symbol,
            evidence?.StrategyId,
            cancellationToken);

        if (!isExit)
        {
            await RecordEntryAsync(fill, evidence, openTrade, cancellationToken);
            return;
        }

        if (openTrade is null)
        {
            _logger.LogWarning(
                "Trade journal reconciliation required: close fill had no open logical trade. Symbol={Symbol}, OrderId={OrderId}",
                fill.Symbol,
                fill.OrderId);
            return;
        }

        fill.TradeJournalRecordId = openTrade.Id;
        fill.IsEntry = false;
        openTrade.AddExitFill(
            fill.FilledAtUtc,
            fill.Price,
            fill.Size,
            fill.ClosedPnl,
            fill.Fee,
            evidence?.StrategyEvaluationId,
            exitReason ?? evidence?.ExitReason ?? InferExitReason(fill.Direction));

        if (openTrade.Status == TradeLifecycleStatus.Closed)
        {
            await TrySetExcursionsAsync(openTrade, cancellationToken);
        }

        await _repository.UpdateAsync(openTrade, cancellationToken);
        _logger.LogInformation(
            "Applied {Lifecycle} fill to trade journal {TradeId}",
            openTrade.Status,
            openTrade.Id);
    }

    private async Task RecordEntryAsync(
        LiveFill fill,
        TradeExecutionEvidence? evidence,
        TradeJournalRecord? openTrade,
        CancellationToken cancellationToken)
    {
        if (openTrade is null)
        {
            if (evidence is null)
            {
                _logger.LogWarning(
                    "Trade journal reconciliation required: entry fill had no strategy evidence. Symbol={Symbol}, OrderId={OrderId}",
                    fill.Symbol,
                    fill.OrderId);
                return;
            }

            openTrade = TradeJournalRecord.Open(
                fill.UserId,
                evidence.StrategyId,
                evidence.StrategyName,
                evidence.StrategyVersion,
                evidence.ConfigurationIdentity,
                fill.Symbol,
                evidence.Side,
                fill.FilledAtUtc,
                fill.Price,
                fill.Size,
                fill.Fee,
                evidence.Leverage,
                evidence.StrategyEvaluationId,
                evidence.MarketRegime,
                evidence.Timeframe,
                evidence.SourceExchange,
                string.IsNullOrWhiteSpace(fill.GridCycleId) ? openTradeIdFallback(fill) : fill.GridCycleId);
            fill.TradeJournalRecordId = openTrade.Id;
            fill.IsEntry = true;
            await _repository.AddAsync(openTrade, cancellationToken);
            _logger.LogInformation("Created trade journal {TradeId} from first opening fill", openTrade.Id);
            return;
        }

        fill.TradeJournalRecordId = openTrade.Id;
        fill.IsEntry = true;
        openTrade.AddEntryFill(fill.FilledAtUtc, fill.Price, fill.Size, fill.Fee);
        await _repository.UpdateAsync(openTrade, cancellationToken);

        static string openTradeIdFallback(LiveFill value) => $"fill:{value.Id:N}";
    }

    private async Task TrySetExcursionsAsync(
        TradeJournalRecord trade,
        CancellationToken cancellationToken)
    {
        try
        {
            var candles = await _candleRepository.GetCandlesAsync(
                trade.Symbol,
                trade.Timeframe,
                new DateTimeOffset(trade.EntryTimeUtc).ToUnixTimeMilliseconds(),
                new DateTimeOffset(trade.ExitTimeUtc!.Value).ToUnixTimeMilliseconds(),
                trade.SourceExchange,
                cancellationToken);
            if (candles.Count == 0)
            {
                _logger.LogWarning(
                    "MFE/MAE calculation unavailable for trade journal {TradeId}: no persisted candles covered its lifetime",
                    trade.Id);
                return;
            }

            var excursion = TradeExcursionCalculator.Calculate(
                trade.Side,
                trade.EntryPrice,
                trade.EntryQuantity,
                trade.ExitPrice!.Value,
                candles);
            trade.SetExcursions(
                excursion.MfeAmount,
                excursion.MfePercent,
                excursion.MaeAmount,
                excursion.MaePercent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "MFE/MAE calculation failed for trade journal {TradeId}; excursions remain unavailable",
                trade.Id);
        }
    }

    private static TradeExitReason InferExitReason(string direction)
    {
        return direction.Contains("liquid", StringComparison.OrdinalIgnoreCase)
            ? TradeExitReason.Liquidation
            : TradeExitReason.External;
    }
}
