using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Candles.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.Entities;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Services;

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

    public Exchange Exchange => Exchange.Hyperliquid;

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
        long? earliestTimestamp = null;
        long? latestCandleTimestamp = null;
        var consecutiveEmptyBatches = 0;

        try
        {
            var intervalMs = HyperliquidAssetMapper.GetIntervalMs(interval);
            var latestTimestamp = requestStartTime is null
                ? await _candleRepository.GetLatestTimestampAsync(coin, interval, source: "Hyperliquid", cancellationToken)
                : null;

            var cursor = requestStartTime
                ?? latestTimestamp
                ?? defaultStartTime;

            if (requestStartTime is null && latestTimestamp.HasValue)
            {
                cursor += 1;
            }

            _logger.LogInformation(
                "Ingesting {Interval} candles for {Symbol} from {StartDate} to {EndDate}",
                interval,
                coin,
                FormatTimestamp(cursor),
                FormatTimestamp(endTime));

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
                    consecutiveEmptyBatches++;

                    if (consecutiveEmptyBatches >= 3)
                    {
                        var nextDataStart = await FindNextDataStartAsync(
                            coin, interval, batchEnd, endTime, intervalMs, cancellationToken);

                        if (nextDataStart is null)
                        {
                            _logger.LogInformation(
                                "No more data found for {Symbol}/{Interval} between {From} and {To}. Ending interval.",
                                coin,
                                interval,
                                FormatTimestamp(batchEnd),
                                FormatTimestamp(endTime));
                            break;
                        }

                        _logger.LogInformation(
                            "Skipped empty range for {Symbol}/{Interval}. Next data found near {DataStart}",
                            coin,
                            interval,
                            FormatTimestamp(nextDataStart.Value));

                        cursor = nextDataStart.Value;
                        consecutiveEmptyBatches = 0;
                    }
                    else
                    {
                        cursor = batchEnd + 1;
                    }

                    await Task.Delay(_options.BatchDelayMs, cancellationToken);
                    continue;
                }

                consecutiveEmptyBatches = 0;

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
                        candle.NumTrades,
                        source: "Hyperliquid"))
                    .ToList();

                await _candleRepository.BulkInsertAsync(candles, cancellationToken);

                fetched += orderedBatch.Count;
                inserted += candles.Count;
                earliestTimestamp ??= orderedBatch[0].Timestamp;
                latestCandleTimestamp = orderedBatch[^1].Timestamp;

                _logger.LogInformation(
                    "Fetched batch for {Symbol}/{Interval}. Count={Count}, Inserted={Inserted}, CandleDate={CandleDate}",
                    coin,
                    interval,
                    orderedBatch.Count,
                    candles.Count,
                    FormatTimestamp(orderedBatch[^1].Timestamp));

                cursor = nextCursor;
                retryCount = 0;

                await Task.Delay(_options.BatchDelayMs, cancellationToken);
            }

            _logger.LogInformation(
                "Interval {Interval} complete for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Range={Earliest} to {Latest}",
                interval,
                coin,
                fetched,
                inserted,
                earliestTimestamp.HasValue ? FormatTimestamp(earliestTimestamp.Value) : "N/A",
                latestCandleTimestamp.HasValue ? FormatTimestamp(latestCandleTimestamp.Value) : "N/A");

            return new IntervalResult
            {
                Interval = interval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                EarliestCandle = earliestTimestamp.HasValue ? FormatTimestamp(earliestTimestamp.Value) : null,
                LatestCandle = latestCandleTimestamp.HasValue ? FormatTimestamp(latestCandleTimestamp.Value) : null,
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

    private static string FormatTimestamp(long unixMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

    private async Task<long?> FindNextDataStartAsync(
        string coin,
        string interval,
        long searchStart,
        long searchEnd,
        long intervalMs,
        CancellationToken cancellationToken)
    {
        var probe = await _restClient.GetCandleSnapshotsAsync(coin, interval, searchStart, searchEnd, cancellationToken);
        if (probe.Count == 0)
        {
            return null;
        }

        await Task.Delay(_options.BatchDelayMs, cancellationToken);

        var left = searchStart;
        var right = searchEnd;
        var minWindow = PageSize * intervalMs;

        while (right - left > minWindow)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mid = left + ((right - left) / 2);
            var midProbe = await _restClient.GetCandleSnapshotsAsync(coin, interval, left, mid, cancellationToken);

            if (midProbe.Count > 0)
            {
                right = mid;
            }
            else
            {
                left = mid;
            }

            await Task.Delay(_options.BatchDelayMs, cancellationToken);
        }

        return left;
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