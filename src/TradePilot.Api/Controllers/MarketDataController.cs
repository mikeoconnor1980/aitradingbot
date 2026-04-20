using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/market")]
public sealed class MarketDataController : ApiController
{
    private readonly IExchangeResolver _exchangeResolver;

    public MarketDataController(
        IMediator mediator,
        IdentityService identityService,
        IExchangeResolver exchangeResolver)
        : base(mediator, identityService)
    {
        _exchangeResolver = exchangeResolver;
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(MarketInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMarketInfoAsync([FromQuery][Required] string asset, CancellationToken cancellationToken)
    {
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        var result = await Mediator.Send(new GetMarketInfoQuery(asset, exchange), cancellationToken);
        return Ok(result);
    }

    [HttpGet("candles")]
    [ProducesResponseType(typeof(List<CandleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCandlesAsync(
        [FromQuery][Required] string asset,
        [FromQuery][Required] string timeframe,
        [FromQuery] long? endTime,
        CancellationToken cancellationToken)
    {
        var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
        var result = await Mediator.Send(new GetCandlesQuery(asset, timeframe, exchange, endTime), cancellationToken);
        return Ok(result);
    }

    [HttpGet("candles/history")]
    [ProducesResponseType(typeof(List<CandleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistoricalCandlesAsync(
        [FromQuery][Required] string asset,
        [FromQuery][Required] string timeframe,
        [FromQuery] long? endTime,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetHistoricalCandlesQuery(asset, timeframe, endTime, limit),
            cancellationToken);
        return Ok(result);
    }
}