using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Repositories;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Produces("application/json")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly LlmOptions _llmOptions;
    private readonly LlmReviewOptions _llmReviewOptions;

    public ProfileController(
        IUserRepository userRepository,
        IOptions<LlmOptions> llmOptions,
        IOptions<LlmReviewOptions> llmReviewOptions)
    {
        _userRepository = userRepository;
        _llmOptions = llmOptions.Value;
        _llmReviewOptions = llmReviewOptions.Value;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null) return Unauthorized();

        return Ok(BuildResponse(user));
    }

    [HttpPut("network")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateNetwork([FromBody] UpdateNetworkRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null) return Unauthorized();

        try
        {
            user.UpdatePreferredNetwork(request.Network);
            await _userRepository.SaveChangesAsync(cancellationToken);
            return Ok(BuildResponse(user));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new Envelope(ex.Message, "invalid_network"));
        }
    }

    private ProfileResponse BuildResponse(Domain.Entities.User user)
    {
        return new ProfileResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PreferredNetwork,
            new LlmModelsInfo(_llmOptions.ModelName, _llmReviewOptions.ModelName));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed record ProfileResponse(Guid Id, string Email, string DisplayName, string PreferredNetwork, LlmModelsInfo LlmModels);
public sealed record LlmModelsInfo(string Strategy, string Review);
public sealed record UpdateNetworkRequest(string Network);
