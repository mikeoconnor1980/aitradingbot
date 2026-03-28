using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Candles.Models;
using TradingApp.Application.MarketData.Models;
using TradingApp.Domain.Entities;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Infrastructure.Services;

public sealed class CandleIngestionService : ICandleIngestionService
{
    private const int PageSize = 500;
    private static readonly SemaphoreSlim Guard = new(1, 1);

    private readonly IHyperliquidRestClient _restClient;
    private readonly ICandleRepository _candleRepository;
    private readonly CandleIngestionOptions _options;
    private readonly ILogger<CandleIngestionService> _logger;

    public CandleIngestionService(
        IHyperliquidRestClient restClient,
        ICandleRepository candleRepository,
        IOptions<CandleIngestionOptions> options,
        ILogger<CandleIngestionService> logger)
    {
        _restClient = restClient;
        _candleRepository = candleRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guard.Wait(0))
        {
            throw new IngestionAlreadyRunningException();
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

    private async Task<IngestionResult> IngestCoreAsync(IngestionRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(_options.MaxIngestionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var token = linkedCts.Token;

        var coin = HyperliquidAssetMapper.ToCoin(request.Symbol);
        var defaultStartTime = new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds();
        var intervalResults = new List<IntervalResult>();

        _logger.LogInformation(
            "Candle ingestion started for {Symbol} with intervals [{Intervals}]",
            coin,
            string.Join(", ", request.Intervals));

        foreach (var interval in request.Intervals)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogWarning("Candle ingestion cancelled before interval {Interval} for {Symbol} could start", interval, coin);
                break;
            }

            var intervalEndTime = GetEffectiveEndTime(interval, request.EndTime);
            var intervalResult = await IngestIntervalAsync(coin, interval, request.StartTime, intervalEndTime, defaultStartTime, token);
            intervalResults.Add(intervalResult);
        }

        stopwatch.Stop();

        var result = new IngestionResult
        {
            TotalFetched = intervalResults.Sum(x => x.Fetched),
            TotalInserted = intervalResults.Sum(x => x.Inserted),
            TotalSkipped = intervalResults.Sum(x => x.Skipped),
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            Intervals = intervalResults,
        };

        _logger.LogInformation(
            "Candle ingestion completed for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Skipped={Skipped}, ElapsedMs={ElapsedMs}",
            coin,
            result.TotalFetched,
            result.TotalInserted,
            result.TotalSkipped,
            result.ElapsedMs);

        return result;
    }

    private async Task<IntervalResult> IngestIntervalAsync(
        string coin,
        string interval,
        long? requestStartTime,
        long endTime,
        long defaultStartTime,
        CancellationToken cancellationToken)
    {
        var fetched = 0;
        var inserted = 0;
        var retryCount = 0;

        try
        {
            var intervalMs = HyperliquidAssetMapper.GetIntervalMs(interval);
            var latestTimestamp = requestStartTime is null
                ? await _candleRepository.GetLatestTimestampAsync(coin, interval, cancellationToken)
                : null;

            var cursor = requestStartTime
                ?? latestTimestamp
                ?? defaultStartTime;

            if (requestStartTime is null && latestTimestamp.HasValue)
            {
                cursor += 1;
            }

            _logger.LogInformation(
                "Ingesting {Interval} candles for {Symbol} from {StartTime} to {EndTime}",
                interval,
                coin,
                cursor,
                endTime);

            while (cursor < endTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(cursor + (PageSize * intervalMs), endTime);

                List<CandleSnapshotDto> batch;
                try
                {
                    batch = await _restClient.GetCandleSnapshotsAsync(coin, interval, cursor, batchEnd, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && retryCount < _options.MaxRetries)
                {
                    retryCount++;
                    var delayMs = (int)Math.Pow(2, retryCount) * 1000;

                    _logger.LogWarning(
                        ex,
                        "Batch fetch failed for {Symbol}/{Interval} (retry {Retry}/{MaxRetries}). Retrying in {DelayMs}ms",
                        coin,
                        interval,
                        retryCount,
                        _options.MaxRetries,
                        delayMs);

                    await Task.Delay(delayMs, cancellationToken);
                    continue;
                }

                if (batch.Count == 0)
                {
                    cursor = batchEnd + 1;
                    await Task.Delay(_options.BatchDelayMs, cancellationToken);
                    continue;
                }

                var orderedBatch = batch
                    .OrderBy(candle => candle.Timestamp)
                    .ToList();

                var nextCursor = orderedBatch[^1].Timestamp + 1;
                if (nextCursor <= cursor)
                {
                    _logger.LogWarning(
                        "Received non-advancing batch for {Symbol}/{Interval}. CurrentCursor={Cursor}, LastBatchTimestamp={LastBatchTimestamp}. Ending interval to avoid infinite loop.",
                        coin,
                        interval,
                        cursor,
                        orderedBatch[^1].Timestamp);

                    break;
                }

                var candles = orderedBatch
                    .Select(candle => Candle.Create(
                        coin,
                        interval,
                        candle.Timestamp,
                        candle.Open,
                        candle.High,
                        candle.Low,
                        candle.Close,
                        candle.Volume,
                        candle.NumTrades))
                    .ToList();

                await _candleRepository.BulkInsertAsync(candles, cancellationToken);

                fetched += orderedBatch.Count;
                inserted += candles.Count;

                _logger.LogInformation(
                    "Fetched batch for {Symbol}/{Interval}. Count={Count}, Inserted={Inserted}, Cursor={Cursor}",
                    coin,
                    interval,
                    orderedBatch.Count,
                    candles.Count,
                    orderedBatch[^1].Timestamp);

                cursor = nextCursor;
                retryCount = 0;

                await Task.Delay(_options.BatchDelayMs, cancellationToken);
            }

            _logger.LogInformation(
                "Interval {Interval} complete for {Symbol}. Fetched={Fetched}, Inserted={Inserted}",
                interval,
                coin,
                fetched,
                inserted);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Interval {Interval} for {Symbol} was cancelled. Fetched so far={Fetched}, Inserted so far={Inserted}",
                interval,
                coin,
                fetched,
                inserted);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = "Cancelled or timed out",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Interval {Interval} for {Symbol} failed after retries. Fetched so far={Fetched}, Inserted so far={Inserted}",
                interval,
                coin,
                fetched,
                inserted);

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = ex.Message,
            };
        }
    }

    private static long GetEffectiveEndTime(string interval, long? requestedEndTime)
    {
        if (requestedEndTime.HasValue)
        {
            return requestedEndTime.Value;
        }

        var intervalMs = HyperliquidAssetMapper.GetIntervalMs(interval);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var currentIntervalStart = (now / intervalMs) * intervalMs;
        var lastClosedCandleStart = currentIntervalStart - intervalMs;

        return Math.Max(0, lastClosedCandleStart);
    }
}