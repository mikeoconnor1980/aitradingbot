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
    public async Task<IActionResult> GetCoverageAsync(CancellationToken cancellationToken)
    {
        var symbols = BinanceAssetMapper.ValidSymbols.ToList();
        var intervals = BinanceAssetMapper.ValidIntervals.ToList();

        var result = await Mediator.Send(
            new GetAllCandleCoverageQuery(symbols, intervals),
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
                $"Unknown symbol '{request.Symbol}'. Supported: BTC, ETH, SOL, DOGE, AVAX, ARB, LINK, OP");
        }

        foreach (var interval in request.Intervals)
        {
            if (!HyperliquidAssetMapper.IsValidTimeframe(interval))
            {
                throw new DomainException(
                    $"Invalid interval '{interval}'. Supported: 5m, 15m, 1h, 4h");
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