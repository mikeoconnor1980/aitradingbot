using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;
using TradePilot.Persistence;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/notifications/telegram")]
[Produces("application/json")]
[Authorize]
public sealed class TelegramLinkController : ControllerBase
{
    private readonly TradePilotDbContext _db;
    private readonly IdentityService _identity;
    private readonly TelegramOptions _telegramOptions;
    private readonly ITelegramNotifier _notifier;

    public TelegramLinkController(TradePilotDbContext db, IdentityService identity, IOptions<TelegramOptions> telegramOptions, ITelegramNotifier notifier)
    {
        _db = db;
        _identity = identity;
        _telegramOptions = telegramOptions.Value;
        _notifier = notifier;
    }

    [HttpPost("link-code")]
    [ProducesResponseType(typeof(LinkCodeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateLinkCode()
    {
        var userId = Guid.Parse(_identity.Identity.UserId);

        // Invalidate any existing unused codes for this user
        var existingCodes = await _db.TelegramLinkCodes
            .Where(c => c.UserId == userId && !c.IsUsed)
            .ToListAsync();

        foreach (var existing in existingCodes)
        {
            existing.MarkUsed();
        }

        var linkCode = TelegramLinkCode.Create(userId);
        _db.TelegramLinkCodes.Add(linkCode);
        await _db.SaveChangesAsync();

        return Ok(new LinkCodeResponse
        {
            Code = linkCode.Code,
            ExpiresAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(linkCode.ExpiresAtUtc),
            BotUsername = _telegramOptions.BotUsername,
        });
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(TelegramStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        var userId = Guid.Parse(_identity.Identity.UserId);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(new TelegramStatusResponse
        {
            Linked = user.TelegramChatId.HasValue,
            ChatId = user.TelegramChatId,
        });
    }

    [HttpDelete("link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unlink()
    {
        var userId = Guid.Parse(_identity.Identity.UserId);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound();
        }

        user.UnlinkTelegram();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendTestMessage()
    {
        var userId = Guid.Parse(_identity.Identity.UserId);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.TelegramChatId is not { } chatId)
        {
            return BadRequest(new { error = "Telegram not linked" });
        }

        await _notifier.NotifyStrategyEventAsync(chatId, "test", "Test Notification",
            "If you see this, Telegram notifications are working!");

        return Ok(new { message = "Test notification sent" });
    }
}

public sealed class LinkCodeResponse
{
    public required string Code { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public string BotUsername { get; init; } = string.Empty;
}

public sealed class TelegramStatusResponse
{
    public bool Linked { get; init; }
    public long? ChatId { get; init; }
}
