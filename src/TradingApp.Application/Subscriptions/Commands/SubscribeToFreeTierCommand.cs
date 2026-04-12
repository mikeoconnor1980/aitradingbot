using MediatR;
using TradingApp.Application.Abstractions.Commands;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.Subscriptions.Commands;

public sealed record SubscribeToFreeTierCommand(Guid UserId) : CreateCommand;

public sealed class SubscribeToFreeTierCommandHandler : IRequestHandler<SubscribeToFreeTierCommand, Guid>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscribeToFreeTierCommandHandler(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Guid> Handle(SubscribeToFreeTierCommand request, CancellationToken cancellationToken)
    {
        var existing = await _subscriptionRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);

        if (existing is not null && !existing.IsExpired(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
        {
            throw new DomainException("You already have an active subscription.");
        }

        if (existing is not null && existing.IsExpired(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
        {
            existing.Expire();
            await _subscriptionRepository.SaveChangesAsync(cancellationToken);
        }

        var subscription = Subscription.Create(
            request.UserId,
            SubscriptionTier.Free,
            Subscription.FreeTierDurationDays);

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        return subscription.Id;
    }
}
