using Microsoft.AspNetCore.Mvc;
using TradingApp.Application.Abstractions.Repositories;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/live-trading")]
[Produces("application/json")]
public sealed class LiveTradingController : ControllerBase
{
    private readonly ILiveFillRepository _fillRepository;
    private readonly IGridCycleRepository _gridCycleRepository;
    private readonly ILiveOrderRepository _orderRepository;

    public LiveTradingController(
        ILiveFillRepository fillRepository,
        IGridCycleRepository gridCycleRepository,
        ILiveOrderRepository orderRepository)
    {
        _fillRepository = fillRepository;
        _gridCycleRepository = gridCycleRepository;
        _orderRepository = orderRepository;
    }

    [HttpGet("fills")]
    [ProducesResponseType(typeof(List<LiveFillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFills(
        [FromQuery] string symbol,
        [FromQuery] DateTime? since = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var fills = await _fillRepository.GetBySymbolAsync(
            symbol, since ?? DateTime.UtcNow.AddDays(-7), limit, cancellationToken);

        var dtos = fills.Select(f => new LiveFillDto(
            f.Id,
            f.OrderId,
            f.Symbol,
            f.Side.ToString(),
            f.Direction,
            f.Price,
            f.Size,
            f.Fee,
            f.ClosedPnl,
            f.FilledAtUtc,
            f.UserId)).ToList();

        return Ok(dtos);
    }

    [HttpGet("grid-cycles")]
    [ProducesResponseType(typeof(List<GridCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGridCycles(
        [FromQuery] string symbol,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var cycles = await _gridCycleRepository.GetBySymbolAsync(
            symbol, null, limit, cancellationToken);

        var dtos = cycles.Select(c => new GridCycleDto(
            c.Id,
            c.GridCycleId,
            c.StrategyName,
            c.Symbol,
            c.AnchorPrice,
            c.TotalLevels,
            c.FilledLevels,
            c.Lifecycle,
            c.StartedAtUtc,
            c.ClosedAtUtc,
            c.CloseReason,
            c.RealisedPnl)).ToList();

        return Ok(dtos);
    }

    [HttpGet("grid-cycles/{gridCycleId}/orders")]
    [ProducesResponseType(typeof(List<LiveOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrdersForCycle(
        string gridCycleId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetByGridCycleIdAsync(
            gridCycleId, cancellationToken);

        var dtos = orders.Select(o => new LiveOrderDto(
            o.Id,
            o.OrderId,
            o.GridCycleId,
            o.Level,
            o.Symbol,
            o.Side.ToString(),
            o.OrderType,
            o.Price,
            o.Size,
            o.TradeType,
            o.Status.ToString(),
            o.PlacedAtUtc,
            o.FilledAtUtc,
            o.CancelledAtUtc)).ToList();

        return Ok(dtos);
    }
}

public sealed record LiveFillDto(
    Guid Id,
    string OrderId,
    string Symbol,
    string Side,
    string Direction,
    decimal Price,
    decimal Size,
    decimal Fee,
    decimal ClosedPnl,
    DateTime FilledAtUtc,
    string UserId);

public sealed record GridCycleDto(
    Guid Id,
    string GridCycleId,
    string StrategyName,
    string Symbol,
    decimal AnchorPrice,
    int TotalLevels,
    int FilledLevels,
    string Lifecycle,
    DateTime StartedAtUtc,
    DateTime? ClosedAtUtc,
    string? CloseReason,
    decimal? RealisedPnl);

public sealed record LiveOrderDto(
    Guid Id,
    string OrderId,
    string GridCycleId,
    int Level,
    string Symbol,
    string Side,
    string OrderType,
    decimal Price,
    decimal Size,
    string TradeType,
    string Status,
    DateTime PlacedAtUtc,
    DateTime? FilledAtUtc,
    DateTime? CancelledAtUtc);
