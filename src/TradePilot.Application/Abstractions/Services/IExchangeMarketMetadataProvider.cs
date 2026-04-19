using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeMarketMetadataProvider
{
    Exchange Exchange { get; }

    Task<MarketInfoDto?> GetMarketInfoAsync(TradingPair pair, CancellationToken cancellationToken = default);

    Task<int?> GetMaxLeverageAsync(TradingPair pair, CancellationToken cancellationToken = default);
}