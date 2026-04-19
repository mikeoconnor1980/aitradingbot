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
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Services;

public sealed class BinanceCandleIngestionService : ICandleIngestionService
{
    private const string BinanceSource = "Binance";
    private const string MarkPriceIntervalPrefix = "mark-";
    private static readonly SemaphoreSlim Guard = new(1, 1);

    private readonly IBinanceFuturesRestClient _restClient;
    private readonly ICandleRepository _candleRepository;
    private readonly BinanceIngestionOptions _options;
    private readonly ILogger<BinanceCandleIngestionService> _logger;

    public BinanceCandleIngestionService(
        IBinanceFuturesRestClient restClient,
        ICandleRepository candleRepository,
        IOptions<BinanceIngestionOptions> options,
        ILogger<BinanceCandleIngestionService> logger)
    {
        _restClient = restClient;
        _candleRepository = candleRepository;
        _options = options.Value;
        _logger = logger;
    }

    public Exchange Exchange => Exchange.Binance;

    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guard.Wait(0))
        {
            throw new IngestionAlreadyRunningException("Binance candle ingestion is already running.");
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

        var displaySymbol = request.Symbol.ToUpperInvariant();
        var futuresSymbol = BinanceAssetMapper.ToFuturesSymbol(displaySymbol);
        var defaultStartTime = new DateTimeOffset(_options.DefaultStartDate).ToUnixTimeMilliseconds();
        var intervalResults = new List<IntervalResult>();

        _logger.LogInformation(
            "Binance candle ingestion started for {Symbol} with intervals [{Intervals}]",
            displaySymbol,
            string.Join(", ", request.Intervals));

        foreach (var interval in request.Intervals)
        {
            if (token.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Binance candle ingestion cancelled before interval {Interval} for {Symbol} could start",
                    interval,
                    displaySymbol);
                break;
            }

            var intervalEndTime = GetEffectiveEndTime(interval, request.EndTime);
            var intervalResult = await IngestIntervalAsync(
                displaySymbol,
                futuresSymbol,
                interval,
                interval,
                request.StartTime,
                intervalEndTime,
                defaultStartTime,
                useMarkPrice: false,
                token);

            intervalResults.Add(intervalResult);
        }

        if (request.IncludeMarkPrice)
        {
            foreach (var interval in request.Intervals)
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Binance mark price ingestion cancelled before interval {Interval} for {Symbol} could start",
                        interval,
                        displaySymbol);
                    break;
                }

                var markPriceInterval = $"{MarkPriceIntervalPrefix}{interval}";
                var intervalEndTime = GetEffectiveEndTime(markPriceInterval, request.EndTime);
                var markPriceResult = await IngestIntervalAsync(
                    displaySymbol,
                    futuresSymbol,
                    markPriceInterval,
                    interval,
                    request.StartTime,
                    intervalEndTime,
                    defaultStartTime,
                    useMarkPrice: true,
                    token);

                intervalResults.Add(markPriceResult);
            }
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
            "Binance candle ingestion completed for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Skipped={Skipped}, ElapsedMs={ElapsedMs}",
            displaySymbol,
            result.TotalFetched,
            result.TotalInserted,
            result.TotalSkipped,
            result.ElapsedMs);

        return result;
    }

    private async Task<IntervalResult> IngestIntervalAsync(
        string displaySymbol,
        string futuresSymbol,
        string storageInterval,
        string fetchInterval,
        long? requestStartTime,
        long endTime,
        long defaultStartTime,
        bool useMarkPrice,
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
            var intervalMs = BinanceAssetMapper.GetIntervalMs(fetchInterval);
            var latestTimestamp = requestStartTime is null
                ? await _candleRepository.GetLatestTimestampAsync(displaySymbol, storageInterval, BinanceSource, cancellationToken)
                : null;

            var cursor = requestStartTime
                ?? latestTimestamp
                ?? defaultStartTime;

            if (requestStartTime is null && latestTimestamp.HasValue)
            {
                cursor += 1;
            }

            _logger.LogInformation(
                "Ingesting Binance {Interval} candles for {Symbol} from {StartDate} to {EndDate}",
                storageInterval,
                displaySymbol,
                FormatTimestamp(cursor),
                FormatTimestamp(endTime));

            while (cursor < endTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(cursor + (_options.PageSize * intervalMs), endTime);

                IReadOnlyList<CandleSnapshotDto> batch;
                try
                {
                    batch = await FetchKlinesAsync(
                        futuresSymbol,
                        fetchInterval,
                        cursor,
                        batchEnd,
                        useMarkPrice,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && retryCount < _options.MaxRetries)
                {
                    retryCount++;
                    var delayMs = (int)Math.Pow(2, retryCount) * 1000;

                    _logger.LogWarning(
                        ex,
                        "Binance batch fetch failed for {Symbol}/{Interval} (retry {Retry}/{MaxRetries}). Retrying in {DelayMs}ms",
                        displaySymbol,
                        storageInterval,
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
                            futuresSymbol,
                            fetchInterval,
                            batchEnd,
                            endTime,
                            intervalMs,
                            useMarkPrice,
                            cancellationToken);

                        if (nextDataStart is null)
                        {
                            _logger.LogInformation(
                                "No more Binance data found for {Symbol}/{Interval} between {From} and {To}. Ending interval.",
                                displaySymbol,
                                storageInterval,
                                FormatTimestamp(batchEnd),
                                FormatTimestamp(endTime));
                            break;
                        }

                        _logger.LogInformation(
                            "Skipped empty Binance range for {Symbol}/{Interval}. Next data found near {DataStart}",
                            displaySymbol,
                            storageInterval,
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
                        "Received non-advancing Binance batch for {Symbol}/{Interval}. CurrentCursor={Cursor}, LastBatchTimestamp={LastBatchTimestamp}. Ending interval to avoid infinite loop.",
                        displaySymbol,
                        storageInterval,
                        cursor,
                        orderedBatch[^1].Timestamp);

                    break;
                }

                var candles = orderedBatch
                    .Select(candle => Candle.Create(
                        displaySymbol,
                        storageInterval,
                        candle.Timestamp,
                        candle.Open,
                        candle.High,
                        candle.Low,
                        candle.Close,
                        candle.Volume,
                        candle.NumTrades,
                        source: BinanceSource))
                    .ToList();

                await _candleRepository.BulkInsertAsync(candles, cancellationToken);

                fetched += orderedBatch.Count;
                inserted += candles.Count;
                earliestTimestamp ??= orderedBatch[0].Timestamp;
                latestCandleTimestamp = orderedBatch[^1].Timestamp;

                _logger.LogInformation(
                    "Fetched Binance batch for {Symbol}/{Interval}. Count={Count}, Inserted={Inserted}, CandleDate={CandleDate}",
                    displaySymbol,
                    storageInterval,
                    orderedBatch.Count,
                    candles.Count,
                    FormatTimestamp(orderedBatch[^1].Timestamp));

                cursor = nextCursor;
                retryCount = 0;

                await Task.Delay(_options.BatchDelayMs, cancellationToken);
            }

            _logger.LogInformation(
                "Binance interval {Interval} complete for {Symbol}. Fetched={Fetched}, Inserted={Inserted}, Range={Earliest} to {Latest}",
                storageInterval,
                displaySymbol,
                fetched,
                inserted,
                earliestTimestamp.HasValue ? FormatTimestamp(earliestTimestamp.Value) : "N/A",
                latestCandleTimestamp.HasValue ? FormatTimestamp(latestCandleTimestamp.Value) : "N/A");

            return new IntervalResult
            {
                Interval = storageInterval,
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
                "Binance interval {Interval} for {Symbol} was cancelled. Fetched so far={Fetched}, Inserted so far={Inserted}",
                storageInterval,
                displaySymbol,
                fetched,
                inserted);

            return new IntervalResult
            {
                Interval = storageInterval,
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
                "Binance interval {Interval} for {Symbol} failed after retries. Fetched so far={Fetched}, Inserted so far={Inserted}",
                storageInterval,
                displaySymbol,
                fetched,
                inserted);

            return new IntervalResult
            {
                Interval = storageInterval,
                Fetched = fetched,
                Inserted = inserted,
                Skipped = fetched - inserted,
                Error = ex.Message,
            };
        }
    }

    private async Task<long?> FindNextDataStartAsync(
        string futuresSymbol,
        string interval,
        long searchStart,
        long searchEnd,
        long intervalMs,
        bool useMarkPrice,
        CancellationToken cancellationToken)
    {
        var probe = await FetchKlinesAsync(
            futuresSymbol,
            interval,
            searchStart,
            searchEnd,
            useMarkPrice,
            cancellationToken);

        if (probe.Count == 0)
        {
            return null;
        }

        await Task.Delay(_options.BatchDelayMs, cancellationToken);

        var left = searchStart;
        var right = searchEnd;
        var minWindow = _options.PageSize * intervalMs;

        while (right - left > minWindow)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mid = left + ((right - left) / 2);
            var midProbe = await FetchKlinesAsync(
                futuresSymbol,
                interval,
                left,
                mid,
                useMarkPrice,
                cancellationToken);

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

    private Task<IReadOnlyList<CandleSnapshotDto>> FetchKlinesAsync(
        string futuresSymbol,
        string interval,
        long startTime,
        long? endTime,
        bool useMarkPrice,
        CancellationToken cancellationToken)
    {
        return useMarkPrice
            ? _restClient.GetMarkPriceKlinesAsync(futuresSymbol, interval, startTime, endTime, _options.PageSize, cancellationToken)
            : _restClient.GetKlinesAsync(futuresSymbol, interval, startTime, endTime, _options.PageSize, cancellationToken);
    }

    private static long GetEffectiveEndTime(string interval, long? requestedEndTime)
    {
        if (requestedEndTime.HasValue)
        {
            return requestedEndTime.Value;
        }

        var intervalMs = BinanceAssetMapper.GetIntervalMs(interval);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var currentIntervalStart = (now / intervalMs) * intervalMs;
        var lastClosedCandleStart = currentIntervalStart - intervalMs;

        return Math.Max(0, lastClosedCandleStart);
    }

    private static string FormatTimestamp(long unixMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
}