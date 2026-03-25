using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Api.Models;
using TradingApp.Api.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IHyperliquidOrderService _orderService;

    public OrdersController(IHyperliquidOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PlaceOrderAsync([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await _orderService.PlaceOrderAsync(request, ct);

        if (!result.Success)
        {
            return BadRequest(new Envelope(result.Detail ?? "Order rejected"));
        }

        return Ok(result);
    }

    [HttpPost("test-sign")]
    [ProducesResponseType(typeof(TestSignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestSignAsync(CancellationToken ct)
    {
        var result = await _orderService.TestSignAsync(ct);
        return Ok(result);
    }
}