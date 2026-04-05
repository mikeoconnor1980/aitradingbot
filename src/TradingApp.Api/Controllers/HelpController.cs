using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Help.Models;
using TradingApp.Application.Help.Queries;

namespace TradingApp.Api.Controllers;

[Route("api/help")]
public sealed class HelpController : ApiController
{
    public HelpController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpPost("chat")]
    [ProducesResponseType(typeof(HelpChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChatAsync(
        [FromBody] HelpChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var result = await Mediator.Send(new HelpChatQuery(request.Question), cancellationToken);
        return Ok(result);
    }
}
