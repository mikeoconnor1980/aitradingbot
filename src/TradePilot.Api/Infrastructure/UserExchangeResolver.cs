using Microsoft.Extensions.Primitives;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Infrastructure;

public sealed class UserExchangeResolver : IExchangeResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IdentityService _identityService;
    private readonly IUserRepository _userRepository;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Exchange? _cached;

    public UserExchangeResolver(
        IHttpContextAccessor httpContextAccessor,
        IdentityService identityService,
        IUserRepository userRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _identityService = identityService;
        _userRepository = userRepository;
    }

    public async Task<Exchange> GetCurrentExchangeAsync(CancellationToken cancellationToken = default)
    {
        if (_cached.HasValue)
        {
            return _cached.Value;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached.HasValue)
            {
                return _cached.Value;
            }

            if (TryParseExchange(_httpContextAccessor.HttpContext?.Request.Headers["X-Exchange"], out var headerExchange))
            {
                _cached = headerExchange;
                return headerExchange;
            }

            var identity = _identityService.Identity;
            if (Guid.TryParse(identity.UserId, out var userId))
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (TryParseExchange(user?.PreferredExchange, out var userExchange))
                {
                    _cached = userExchange;
                    return userExchange;
                }
            }

            _cached = Exchange.Hyperliquid;
            return _cached.Value;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool TryParseExchange(StringValues values, out Exchange exchange)
    {
        return TryParseExchange(values.FirstOrDefault(), out exchange);
    }

    private static bool TryParseExchange(string? value, out Exchange exchange)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out exchange))
        {
            return true;
        }

        exchange = Exchange.Hyperliquid;
        return false;
    }
}