using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.FundingRates.Commands;
using TradingApp.Application.FundingRates.Models;
using TradingApp.Infrastructure.Binance;

namespace TradingApp.Api.Controllers;

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