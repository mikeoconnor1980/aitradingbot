using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Candles.Commands;
using TradingApp.Application.Candles.Models;
using TradingApp.Infrastructure.Hyperliquid;

namespace TradingApp.Api.Controllers;

[Route("api/candles")]
public sealed class CandlesController : ApiController
{
    public CandlesController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
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
}