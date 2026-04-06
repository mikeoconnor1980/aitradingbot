using Microsoft.AspNetCore.Mvc;
using TradingApp.Application.Abstractions.Services;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Produces("application/json")]
public sealed class WalletController : ControllerBase
{
    private readonly ISignerProvider _signerProvider;

    public WalletController(ISignerProvider signerProvider)
    {
        _signerProvider = signerProvider;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(WalletStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var isConfigured = _signerProvider.IsConfigured;
        return Ok(new WalletStatusResponse(
            IsConfigured: isConfigured,
            WalletAddress: isConfigured ? _signerProvider.WalletAddress : null));
    }

    [HttpPost("configure")]
    [ProducesResponseType(typeof(WalletConfiguredResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Configure([FromBody] ConfigureWalletRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PrivateKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid private key",
                Detail = "Private key is required."
            });
        }

        try
        {
            _signerProvider.Configure(request.PrivateKey);
            return Ok(new WalletConfiguredResponse(
                WalletAddress: _signerProvider.WalletAddress));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid private key",
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("configure")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Disconnect()
    {
        _signerProvider.Clear();
        return NoContent();
    }
}

public sealed record ConfigureWalletRequest(string PrivateKey);

public sealed record WalletStatusResponse(bool IsConfigured, string? WalletAddress);

public sealed record WalletConfiguredResponse(string WalletAddress);
