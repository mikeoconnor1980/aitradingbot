using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.StrategyAuthoring.Commands;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Queries;
using TradingApp.Application.StrategyAuthoring.Validation;

namespace TradingApp.Api.Controllers;

[Route("api/strategies")]
public sealed class StrategiesController : ApiController
{
    private readonly IStrategyValidator _validator;

    public StrategiesController(
        IMediator mediator,
        IdentityService identityService,
        IStrategyValidator validator)
        : base(mediator, identityService)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public IActionResult Validate([FromBody] StrategyConfig config)
    {
        var result = _validator.Validate(config);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StrategySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategies(CancellationToken cancellationToken)
    {
        var strategies = await Mediator.Send(new GetStrategiesQuery(IdentityService.Identity), cancellationToken);
        return Ok(strategies);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStrategy(Guid id, CancellationToken cancellationToken)
    {
        var strategy = await Mediator.Send(new GetStrategyByIdQuery(id, IdentityService.Identity), cancellationToken);
        return Ok(strategy);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStrategy([FromBody] StrategyConfig config, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(new CreateStrategyCommand(config, IdentityService.Identity), cancellationToken);
        return CreatedAtAction(nameof(GetStrategy), new { id }, new CreatedResultEnvelope(id));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStrategy(
        Guid id,
        [FromBody] StrategyConfig config,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateStrategyCommand(id, config, IdentityService.Identity), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStrategy(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteStrategyCommand(id, IdentityService.Identity), cancellationToken);
        return NoContent();
    }
}