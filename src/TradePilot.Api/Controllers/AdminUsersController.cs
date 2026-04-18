using MediatR;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Administration.Commands;
using TradePilot.Application.Administration.Models;
using TradePilot.Application.Administration.Queries;

namespace TradePilot.Api.Controllers;

[Route("api/admin/users")]
public sealed class AdminUsersController : ApiController
{
    public AdminUsersController(IMediator mediator, IdentityService identityService)
        : base(mediator, identityService)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminUsers(CancellationToken cancellationToken)
    {
        var admins = await Mediator.Send(new GetAdminUsersQuery(IdentityService.Identity), cancellationToken);
        return Ok(admins);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddAdminUser([FromBody] CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(new AddAdminUserCommand(request.Email, IdentityService.Identity), cancellationToken);
        return CreatedAtAction(nameof(GetAdminUsers), new { }, new CreatedResultEnvelope(id));
    }

    [HttpDelete("{grantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveAdminUser(Guid grantId, CancellationToken cancellationToken)
    {
        await Mediator.Send(new RemoveAdminUserCommand(grantId, IdentityService.Identity), cancellationToken);
        return NoContent();
    }
}