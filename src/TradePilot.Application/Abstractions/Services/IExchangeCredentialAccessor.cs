namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeCredentialAccessor
{
    Task<ExchangeCredentialSnapshot?> GetActiveCredentialAsync(Exchange exchange, CancellationToken cancellationToken = default);
}

public sealed record ExchangeCredentialSnapshot(Exchange Exchange, string ApiKey, string ApiSecret, string Label);