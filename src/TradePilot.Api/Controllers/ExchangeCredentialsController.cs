using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/credentials")]
[Produces("application/json")]
[Authorize]
public sealed class ExchangeCredentialsController : ControllerBase
{
    private readonly IUserExchangeCredentialRepository _credentialRepository;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IBinanceFuturesAuthClient _binanceAuthClient;

    public ExchangeCredentialsController(
        IUserExchangeCredentialRepository credentialRepository,
        ICredentialEncryptionService credentialEncryptionService,
        IBinanceFuturesAuthClient binanceAuthClient)
    {
        _credentialRepository = credentialRepository;
        _credentialEncryptionService = credentialEncryptionService;
        _binanceAuthClient = binanceAuthClient;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExchangeCredentialResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var credentials = await _credentialRepository.GetAllActiveByUserIdAsync(userId.Value, cancellationToken);
        return Ok(credentials.Select(ToResponse).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExchangeCredentialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetOrUpdate([FromBody] SetExchangeCredentialRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<Exchange>(request.Exchange, true, out var exchange))
        {
            return BadRequest(new Envelope("Unsupported exchange.", "invalid_exchange"));
        }

        if (exchange == Exchange.Hyperliquid)
        {
            return BadRequest(new Envelope("API credentials are only supported for key-based exchanges.", "invalid_exchange"));
        }

        try
        {
            var encryptedSecret = _credentialEncryptionService.Encrypt(request.ApiSecret);
            var existing = await _credentialRepository.GetActiveByUserIdAndExchangeAsync(userId.Value, exchange, cancellationToken);

            if (existing is not null)
            {
                existing.UpdateSecrets(request.ApiKey, encryptedSecret, request.Label);
                await _credentialRepository.SaveChangesAsync(cancellationToken);
                return Ok(ToResponse(existing));
            }

            var credential = UserExchangeCredential.Create(userId.Value, exchange, request.ApiKey, encryptedSecret, request.Label);
            await _credentialRepository.AddAsync(credential, cancellationToken);
            return Ok(ToResponse(credential));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new Envelope(ex.Message, "invalid_credential"));
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var credential = await _credentialRepository.GetByIdAsync(id, cancellationToken);
        if (credential is null || credential.UserId != userId.Value || !credential.IsActive)
        {
            return NotFound(new Envelope("No credential configured.", "no_credential"));
        }

        credential.Deactivate();
        await _credentialRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{exchange}/test")]
    [ProducesResponseType(typeof(ExchangeCredentialConnectionTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestConnection(string exchange, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Exchange>(exchange, true, out var parsedExchange))
        {
            return BadRequest(new Envelope("Unsupported exchange.", "invalid_exchange"));
        }

        if (parsedExchange != Exchange.Binance)
        {
            return BadRequest(new Envelope("Connection testing is only supported for Binance credentials.", "invalid_exchange"));
        }

        await _binanceAuthClient.GetBalancesAsync(cancellationToken);

        return Ok(new ExchangeCredentialConnectionTestResponse(parsedExchange.ToString(), true));
    }

    private static ExchangeCredentialResponse ToResponse(UserExchangeCredential credential)
    {
        return new ExchangeCredentialResponse(
            credential.Id,
            credential.Exchange.ToString(),
            credential.ApiKey,
            MaskSecret(credential.EncryptedApiSecret),
            credential.Label,
            credential.CreatedAtUtc,
            credential.IsActive);
    }

    private static string MaskSecret(string value)
    {
        var suffix = value.Length <= 4 ? value : value[^4..];
        return $"****{suffix}";
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed record SetExchangeCredentialRequest(string Exchange, string ApiKey, string ApiSecret, string Label);
public sealed record ExchangeCredentialResponse(Guid Id, string Exchange, string ApiKey, string MaskedSecret, string Label, long CreatedAtUtc, bool IsActive);
public sealed record ExchangeCredentialConnectionTestResponse(string Exchange, bool Success);