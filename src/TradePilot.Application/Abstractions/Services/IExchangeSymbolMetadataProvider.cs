namespace TradePilot.Application.Abstractions.Services;

/// <summary>
/// Exchange-agnostic symbol metadata provider.
/// Implementations exist per exchange.
/// </summary>
public interface IExchangeSymbolMetadataProvider
{
    Task<IReadOnlyList<ExchangeSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default);

    Task<ExchangeSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default);
}

/// <summary>
/// Common symbol metadata across supported exchanges.
/// </summary>
public sealed record ExchangeSymbolMetadata(
    string Asset,
    string Symbol,
    int SizeDecimals,
    int PriceDecimals,
    int MaxLeverage);