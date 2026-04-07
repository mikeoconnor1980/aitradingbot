using TradingApp.Application.MarketData.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Trading.Services;

public interface IFillProcessor
{
    Task ProcessFillAsync(FillEventDto fill, CancellationToken cancellationToken = default);

    Task ProcessOrderUpdateAsync(OrderUpdateDto update, CancellationToken cancellationToken = default);
}
