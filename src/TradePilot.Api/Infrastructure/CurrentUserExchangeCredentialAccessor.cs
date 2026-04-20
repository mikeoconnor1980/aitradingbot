using System.Collections.Concurrent;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Infrastructure;

public sealed class CurrentUserExchangeCredentialAccessor : IExchangeCredentialAccessor
{
    private static readonly object CacheKey = new();

    private readonly IdentityService _identityService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;

    public CurrentUserExchangeCredentialAccessor(
        IdentityService identityService,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory)
    {
        _identityService = identityService;
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
    }

    public async Task<ExchangeCredentialSnapshot?> GetActiveCredentialAsync(
        Exchange exchange,
        CancellationToken cancellationToken = default)
    {
        if (exchange == Exchange.Hyperliquid)
        {
            return null;
        }

        var identity = _identityService.Identity;
        if (!Guid.TryParse(identity.UserId, out var userId))
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var cache = GetOrCreateCache(httpContext);
        if (cache.TryGetValue(exchange, out var cachedSnapshot))
        {
            return await cachedSnapshot.Value;
        }

        var requestCancellationToken = httpContext?.RequestAborted ?? cancellationToken;
        var lazySnapshot = cache.GetOrAdd(
            exchange,
            _ => new Lazy<Task<ExchangeCredentialSnapshot?>>(
                () => ResolveCredentialSnapshotAsync(userId, exchange, requestCancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return await lazySnapshot.Value;
    }

    private async Task<ExchangeCredentialSnapshot?> ResolveCredentialSnapshotAsync(
        Guid userId,
        Exchange exchange,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var credentialRepository = scope.ServiceProvider.GetRequiredService<IUserExchangeCredentialRepository>();
        var credentialEncryptionService = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();

        var credential = await credentialRepository.GetActiveByUserIdAndExchangeAsync(userId, exchange, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        return new ExchangeCredentialSnapshot(
            credential.Exchange,
            credential.ApiKey,
            credentialEncryptionService.Decrypt(credential.EncryptedApiSecret),
            credential.Label);
    }

    private static ConcurrentDictionary<Exchange, Lazy<Task<ExchangeCredentialSnapshot?>>> GetOrCreateCache(HttpContext? httpContext)
    {
        if (httpContext?.Items[CacheKey] is ConcurrentDictionary<Exchange, Lazy<Task<ExchangeCredentialSnapshot?>>> existingCache)
        {
            return existingCache;
        }

        var newCache = new ConcurrentDictionary<Exchange, Lazy<Task<ExchangeCredentialSnapshot?>>>();
        if (httpContext is not null)
        {
            httpContext.Items[CacheKey] = newCache;
        }

        return newCache;
    }
}