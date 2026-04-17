using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Domain.Entities;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Services;

public sealed class FundingRateIngestionService : IFundingRateIngestionService
{
    private static readonly SemaphoreSlim Guard = new(1, 1);

    private readonly IBinanceFuturesRestClient _restClient;
    private readonly IFundingRateRepository _repository;
    private readonly BinanceIngestionOptions _options;
    private readonly ILogger<FundingRateIngestionService> _logger;

    public FundingRateIngestionService(
        IBinanceFuturesRestClient restClient,
        IFundingRateRepository repository,
        IOptions<BinanceIngestionOptions> options,
        ILogger<FundingRateIngestionService> logger)
    {
        _restClient = restClient;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FundingRateIngestionResult> IngestAsync(
        FundingRateIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guard.Wait(0))
        {
            throw new IngestionAlreadyRunningException("Funding rate ingestion is already running.");
        }

        try
        {
            return await IngestCoreAsync(request, cancellationToken);
        }
        finally
        {
            Guard.Release();
        }
    }

    private async Task<FundingRateIngestionResult> IngestCoreAsync(
        FundingRateIngestionRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(_options.MaxIngestionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        var displaySymbol = request.Symbol.ToUpperInvariant();
        var futuresSymbol = BinanceAssetMapper.ToFuturesSymbol(displaySymbol);
        var defaultStartTime = new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds();
        var effectiveEndTime = request.EndTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var fetched = 0;
        var inserted = 0;
        var retryCount = 0;
        long? earliestTimestamp = null;
        long? latestTimestamp = null;

        try
        {
            var latestStored = request.StartTime is null
                ? await _repository.GetLatestTimestampAsync(displaySymbol, token)
                : null;

            var cursor = request.StartTime
                ?? (latestStored.HasValue ? latestStored.Value + 1 : defaultStartTime);

            _logger.LogInformation(
                "Funding rate ingestion started for {Symbol} from {StartDate} to {EndDate}",
                displaySymbol,
                FormatTimestamp(cursor),
                FormatTimestamp(effectiveEndTime));

            while (cursor < effectiveEndTime)
            {
                token.ThrowIfCancellationRequested();

                IReadOnlyList<FundingRateDto> batch;
                try
                {
                    batch = await _restClient.GetFundingRatesAsync(
                        futuresSymbol,
                        cursor,
                        effectiveEndTime,
                        limit: 1000,
                        cancellationToken: token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && retryCount < _options.MaxRetries)
                {
                    retryCount++;
                    var delayMs = (int)Math.Pow(2, retryCount) * 1000;

                    _logger.LogWarning(
                        ex,
                        "Funding rate batch fetch failed for {Symbol} (retry {Retry}/{MaxRetries}). Retrying in {DelayMs}ms",
                        displaySymbol,
                        retryCount,
                        _options.MaxRetries,
                        delayMs);

                    await Task.Delay(delayMs, token);
                    continue;
                }

                if (batch.Count == 0)
                {
                    break;
                }

                var orderedBatch = batch
                    .OrderBy(rate => rate.FundingTime)
                    .ToList();

                var nextCursor = orderedBatch[^1].FundingTime + 1;
                if (nextCursor <= cursor)
                {
                    _logger.LogWarning(
                        "Received non-advancing funding rate batch for {Symbol}. CurrentCursor={Cursor}, LastFundingTime={LastFundingTime}. Ending to avoid infinite loop.",
                        displaySymbol,
                        cursor,
                        orderedBatch[^1].FundingTime);
                    break;
                }

                var entities = orderedBatch
                    .Select(rate => FundingRate.Create(
                        displaySymbol,
                        rate.FundingTime,
                        rate.Rate,
                        rate.MarkPrice))
                    .ToList();

                await _repository.BulkInsertAsync(entities, token);

                fetched += orderedBatch.Count;
                inserted += entities.Count;
                earliestTimestamp ??= orderedBatch[0].FundingTime;
                latestTimestamp = orderedBatch[^1].FundingTime;

                _logger.LogInformation(
                    "Fetched Binance funding rate batch for {Symbol}. Count={Count}, FundingTime={FundingTime}",
                    displaySymbol,
                    orderedBatch.Count,
                    FormatTimestamp(orderedBatch[^1].FundingTime));

                cursor = nextCursor;
                retryCount = 0;

                if (orderedBatch.Count < 1000)
                {
                    break;
                }

                await Task.Delay(_options.BatchDelayMs, token);
            }

            _logger.LogInformation(
                "Funding rate ingestion completed for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Skipped={Skipped}, ElapsedMs={ElapsedMs}",
                displaySymbol,
                fetched,
                inserted,
                fetched - inserted,
                stopwatch.ElapsedMilliseconds);

            return CreateResult(
                displaySymbol,
                fetched,
                inserted,
                stopwatch.ElapsedMilliseconds,
                earliestTimestamp,
                latestTimestamp,
                error: null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Funding rate ingestion cancelled for {Symbol}. Fetched so far={Fetched}, Inserted so far={Inserted}",
                displaySymbol,
                fetched,
                inserted);

            return CreateResult(
                displaySymbol,
                fetched,
                inserted,
                stopwatch.ElapsedMilliseconds,
                earliestTimestamp,
                latestTimestamp,
                error: "Cancelled or timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Funding rate ingestion failed for {Symbol}. Fetched so far={Fetched}, Inserted so far={Inserted}",
                displaySymbol,
                fetched,
                inserted);

            return CreateResult(
                displaySymbol,
                fetched,
                inserted,
                stopwatch.ElapsedMilliseconds,
                earliestTimestamp,
                latestTimestamp,
                error: ex.Message);
        }
    }

    private static FundingRateIngestionResult CreateResult(
        string symbol,
        int fetched,
        int inserted,
        long elapsedMs,
        long? earliestTimestamp,
        long? latestTimestamp,
        string? error)
    {
        return new FundingRateIngestionResult
        {
            Symbol = symbol,
            TotalFetched = fetched,
            TotalInserted = inserted,
            TotalSkipped = fetched - inserted,
            ElapsedMs = elapsedMs,
            EarliestTimestamp = earliestTimestamp.HasValue ? FormatTimestamp(earliestTimestamp.Value) : null,
            LatestTimestamp = latestTimestamp.HasValue ? FormatTimestamp(latestTimestamp.Value) : null,
            Error = error,
        };
    }

    private static string FormatTimestamp(long unixMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
}