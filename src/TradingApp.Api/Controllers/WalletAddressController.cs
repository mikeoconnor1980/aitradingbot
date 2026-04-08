using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingApp.Api.Infrastructure;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Domain.Entities;

namespace TradingApp.Api.Controllers;

[ApiController]
[Route("api/wallet-address")]
[Produces("application/json")]
[Authorize]
public sealed class WalletAddressController : ControllerBase
{
    private readonly IUserWalletAddressRepository _walletAddressRepository;

    public WalletAddressController(IUserWalletAddressRepository walletAddressRepository)
    {
        _walletAddressRepository = walletAddressRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WalletAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var wallet = await _walletAddressRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken);
        if (wallet is null)
        {
            return NotFound(new Envelope("No wallet address configured.", "no_wallet"));
        }

        return Ok(new WalletAddressResponse(wallet.WalletAddress, wallet.Exchange));
    }

    [HttpPost]
    [ProducesResponseType(typeof(WalletAddressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetOrUpdate([FromBody] SetWalletAddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var existing = await _walletAddressRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken);

        if (existing is not null)
        {
            try
            {
                existing.UpdateAddress(request.WalletAddress);
                await _walletAddressRepository.SaveChangesAsync(cancellationToken);
                return Ok(new WalletAddressResponse(existing.WalletAddress, existing.Exchange));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new Envelope(ex.Message, "invalid_address"));
            }
        }

        try
        {
            var walletAddress = UserWalletAddress.Create(userId.Value, request.WalletAddress);
            await _walletAddressRepository.AddAsync(walletAddress, cancellationToken);
            return Ok(new WalletAddressResponse(walletAddress.WalletAddress, walletAddress.Exchange));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new Envelope(ex.Message, "invalid_address"));
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var existing = await _walletAddressRepository.GetActiveByUserIdAsync(userId.Value, cancellationToken);
        if (existing is null)
        {
            return NotFound(new Envelope("No wallet address configured.", "no_wallet"));
        }

        existing.Deactivate();
        await _walletAddressRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed record SetWalletAddressRequest(string WalletAddress);
public sealed record WalletAddressResponse(string WalletAddress, string Exchange);
