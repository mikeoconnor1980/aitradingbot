using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Candles.Commands;
using TradePilot.Application.Candles.Models;
using TradePilot.Application.Candles.Queries;
using TradePilot.Infrastructure.Binance;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Api.Controllers;

[Route("api/candles")]
public sealed class CandlesController : ApiController
{
    public CandlesController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet("coverage")]
    [ProducesResponseType(typeof(AllCandleCoverageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoverageAsync(
        [FromQuery] string[]? symbols,
        [FromQuery] string[]? intervals,
        CancellationToken cancellationToken)
    {
        var requestedSymbols = symbols is { Length: > 0 }
            ? symbols
            : BinanceAssetMapper.ValidSymbols.ToArray();
        var requestedIntervals = intervals is { Length: > 0 }
            ? intervals
            : BinanceAssetMapper.ValidIntervals.ToArray();

        foreach (var symbol in requestedSymbols)
        {
            if (!BinanceAssetMapper.IsValidSymbol(symbol))
            {
                throw new DomainException(
                    $"Invalid symbol: '{symbol}'. Valid Binance symbols: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
            }
        }

        foreach (var interval in requestedIntervals)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
            {
                throw new DomainException(
                    $"Invalid interval: '{interval}'. Valid Binance intervals: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
            }
        }

        var result = await Mediator.Send(
            new GetAllCandleCoverageQuery(requestedSymbols, requestedIntervals),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("ingest")]
    [ProducesResponseType(typeof(IngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestAsync([FromBody] IngestCandlesRequest request, CancellationToken cancellationToken)
    {
        var coin = HyperliquidAssetMapper.ToCoin(request.Symbol);
        if (!HyperliquidAssetMapper.IsValidCoin(coin))
        {
            throw new DomainException(
                $"Invalid Hyperliquid symbol '{request.Symbol}'. Use an alphanumeric asset name or a HIP-3 asset identifier such as 'XYZ:TSLA'.");
        }

        if (!HyperliquidAssetMapper.GetSupportedCoins().Contains(coin, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Unknown symbol '{request.Symbol}'. Supported: {string.Join(", ", HyperliquidAssetMapper.GetSupportedCoins())}");
        }

        foreach (var interval in request.Intervals)
        {
            if (!HyperliquidAssetMapper.IsValidTimeframe(interval))
            {
                throw new DomainException(
                    $"Invalid interval '{interval}'. Supported: {string.Join(", ", HyperliquidAssetMapper.GetSupportedTimeframes())}");
            }
        }

        var result = await Mediator.Send(
            new IngestCandlesCommand(
                request.Symbol,
                request.Intervals,
                request.StartTime,
                request.EndTime),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("ingest/binance")]
    [ProducesResponseType(typeof(IngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestBinanceAsync(
        [FromBody] IngestCandlesRequest request,
        CancellationToken cancellationToken)
    {
        if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
        {
            throw new DomainException(
                $"Invalid symbol: '{request.Symbol}'. Valid Binance symbols: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
        }

        foreach (var interval in request.Intervals)
        {
            if (!BinanceAssetMapper.IsValidInterval(interval))
            {
                throw new DomainException(
                    $"Invalid interval: '{interval}'. Valid Binance intervals: {string.Join(", ", BinanceAssetMapper.ValidIntervals)}");
            }
        }

        if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime.Value <= request.StartTime.Value)
        {
            throw new DomainException("EndTime must be greater than StartTime.");
        }

        var result = await Mediator.Send(
            new IngestBinanceCandlesCommand(
                new IngestionRequest
                {
                    Symbol = request.Symbol,
                    Intervals = request.Intervals,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    IncludeMarkPrice = request.IncludeMarkPrice,
                }),
            cancellationToken);

        return Ok(result);
    }
}