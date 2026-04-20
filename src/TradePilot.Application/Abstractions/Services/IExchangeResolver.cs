using TradePilot.Domain.Enums;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeResolver
{
    Task<Exchange> GetCurrentExchangeAsync(CancellationToken cancellationToken = default);
}