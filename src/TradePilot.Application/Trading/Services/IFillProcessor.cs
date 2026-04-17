using TradePilot.Application.MarketData.Models;
using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Trading.Services;

public interface IFillProcessor
{
    Task ProcessFillAsync(FillEventDto fill, CancellationToken cancellationToken = default);

    Task ProcessOrderUpdateAsync(OrderUpdateDto update, CancellationToken cancellationToken = default);
}
