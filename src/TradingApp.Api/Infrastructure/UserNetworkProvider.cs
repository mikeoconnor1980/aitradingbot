using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Infrastructure;

/// <summary>
/// Resolves the current user's preferred network from the database.
/// Scoped per-request; caches after first resolution.
/// Falls back to the configured default when no authenticated user or user record is found.
/// </summary>
public sealed class UserNetworkProvider : INetworkProvider
{
    private readonly IdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly HyperliquidOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cached;

    public UserNetworkProvider(
        IdentityService identityService,
        IUserRepository userRepository,
        IOptions<HyperliquidOptions> options)
    {
        _identityService = identityService;
        _userRepository = userRepository;
        _options = options.Value;
    }

    public async Task<string> GetNetworkAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var identity = _identityService.Identity;

            if (Guid.TryParse(identity.UserId, out var userId))
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                _cached = user?.PreferredNetwork ?? _options.Network;
            }
            else
            {
                _cached = _options.Network;
            }
        }
        finally
        {
            _lock.Release();
        }

        return _cached;
    }
}
