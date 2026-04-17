using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Produces("application/json")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly LlmOptions _llmOptions;
    private readonly LlmReviewOptions _llmReviewOptions;

    public ProfileController(
        IUserRepository userRepository,
        ISubscriptionRepository subscriptionRepository,
        IOptions<LlmOptions> llmOptions,
        IOptions<LlmReviewOptions> llmReviewOptions)
    {
        _userRepository = userRepository;
        _subscriptionRepository = subscriptionRepository;
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

        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var isActive = subscription is not null && !subscription.IsExpired(nowMs);

        if (subscription is not null && subscription.IsExpired(nowMs))
        {
            subscription.Expire();
            await _subscriptionRepository.SaveChangesAsync(cancellationToken);
        }

        return Ok(BuildResponse(user, subscription, isActive));
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

            var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken);
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var isActive = subscription is not null && !subscription.IsExpired(nowMs);

            return Ok(BuildResponse(user, subscription, isActive));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new Envelope(ex.Message, "invalid_network"));
        }
    }

    private ProfileResponse BuildResponse(Domain.Entities.User user, Domain.Entities.Subscription? subscription, bool hasActiveSubscription)
    {
        return new ProfileResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PreferredNetwork,
            new LlmModelsInfo(_llmOptions.ModelName, _llmReviewOptions.ModelName),
            hasActiveSubscription,
            subscription?.Tier,
            subscription?.Status,
            subscription?.ExpiresAtUtc);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredNetwork,
    LlmModelsInfo LlmModels,
    bool HasActiveSubscription,
    SubscriptionTier? SubscriptionTier,
    SubscriptionStatus? SubscriptionStatus,
    long? SubscriptionExpiresAtUtc);
public sealed record LlmModelsInfo(string Strategy, string Review);
public sealed record UpdateNetworkRequest(string Network);
