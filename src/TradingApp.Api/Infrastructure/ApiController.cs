using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TradingApp.Api.Infrastructure;

[ApiController]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected IMediator Mediator { get; }
    protected IdentityService IdentityService { get; }

    protected ApiController(IMediator mediator, IdentityService identityService)
    {
        Mediator = mediator;
        IdentityService = identityService;
    }
}