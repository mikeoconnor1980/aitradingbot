using MediatR;
using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.Subscriptions.Commands;

public sealed record SubscribeCommand(Guid UserId, SubscriptionTier Tier) : CreateCommand;

public sealed class SubscribeCommandHandler : IRequestHandler<SubscribeCommand, Guid>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscribeCommandHandler(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Guid> Handle(SubscribeCommand request, CancellationToken cancellationToken)
    {
        var existing = await _subscriptionRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (existing is not null && !existing.IsExpired(nowMs) && existing.Status == SubscriptionStatus.Active)
        {
            throw new DomainException("You already have an active subscription.");
        }

        if (existing is not null && existing.Status == SubscriptionStatus.Active && existing.IsExpired(nowMs))
        {
            existing.Expire();
            await _subscriptionRepository.SaveChangesAsync(cancellationToken);
        }

        var tier = request.Tier == SubscriptionTier.Free ? SubscriptionTier.Beginner : request.Tier;
        var subscription = Subscription.Create(request.UserId, tier, Subscription.TrialDurationDays);

        await _subscriptionRepository.AddAsync(subscription, cancellationToken);

        return subscription.Id;
    }
}

public sealed record CancelSubscriptionCommand(Guid UserId) : Command;

public sealed class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    private readonly ISubscriptionRepository _subscriptionRepository;

    public CancelSubscriptionCommandHandler(ISubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Unit> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
        {
            throw new DomainException("No active subscription found.");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (subscription.IsExpired(nowMs))
        {
            subscription.Expire();
        }
        else
        {
            subscription.Cancel();
        }

        await _subscriptionRepository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}