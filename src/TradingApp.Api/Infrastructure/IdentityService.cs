using TradingApp.Application.Abstractions.Identity;

namespace TradingApp.Api.Infrastructure;

public sealed class IdentityService
{
    public AppIdentity Identity { get; } = new("dev-user", "developer@tradingapp.local");
}