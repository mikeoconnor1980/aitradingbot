using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.FundingRates.Commands;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Api.Controllers;

[Route("api/funding")]
public sealed class FundingRatesController : ApiController
{
    public FundingRatesController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpPost("ingest")]
    [ProducesResponseType(typeof(FundingRateIngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestAsync(
        [FromBody] IngestFundingRatesRequest request,
        CancellationToken cancellationToken)
    {
        if (!BinanceAssetMapper.IsValidSymbol(request.Symbol))
        {
            throw new DomainException(
                $"Invalid symbol: '{request.Symbol}'. Valid Binance symbols: {string.Join(", ", BinanceAssetMapper.ValidSymbols)}");
        }

        if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime.Value <= request.StartTime.Value)
        {
            throw new DomainException("EndTime must be greater than StartTime.");
        }

        var result = await Mediator.Send(
            new IngestFundingRatesCommand(
                new FundingRateIngestionRequest
                {
                    Symbol = request.Symbol,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                }),
            cancellationToken);

        return Ok(result);
    }
}