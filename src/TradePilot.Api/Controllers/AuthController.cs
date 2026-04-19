using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Auth;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IAdminAuthorizationService _adminAuthorizationService;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IPasswordHasher passwordHasher,
        IGoogleTokenValidator googleTokenValidator,
        IAdminAuthorizationService adminAuthorizationService,
        ISubscriptionRepository subscriptionRepository)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _googleTokenValidator = googleTokenValidator;
        _adminAuthorizationService = adminAuthorizationService;
        _subscriptionRepository = subscriptionRepository;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!IsPasswordComplex(request.Password))
        {
            return BadRequest(new Envelope(
                "Password must be at least 8 characters and contain at least one uppercase letter, one number, and one special character.",
                "weak_password"));
        }

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return Conflict(new Envelope("An account with this email already exists.", "duplicate_email"));
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var newUser = Domain.Entities.User.Create(request.Email, request.DisplayName, passwordHash);
        await _userRepository.AddAsync(newUser, cancellationToken);

        var tokens = _jwtTokenService.GenerateTokens(newUser);
        var isAdmin = await _adminAuthorizationService.IsAdminAsync(newUser.Email, cancellationToken);

        return Ok(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            new UserInfo(newUser.Id, newUser.Email, newUser.DisplayName, isAdmin)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new Envelope("Invalid email or password.", "invalid_credentials"));
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return BadRequest(new Envelope(
                "This account uses Google sign-in. Please sign in with Google instead.",
                "external_auth_only"));
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new Envelope("Invalid email or password.", "invalid_credentials"));
        }

        var tokens = _jwtTokenService.GenerateTokens(user);
        var isAdmin = await _adminAuthorizationService.IsAdminAsync(user.Email, cancellationToken);

        return Ok(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            new UserInfo(user.Id, user.Email, user.DisplayName, isAdmin)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        if (result is null)
        {
            return Unauthorized(new Envelope("Invalid or expired refresh token.", "invalid_refresh_token"));
        }

        var (userId, _) = result.Value;
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new Envelope("Invalid or expired refresh token.", "invalid_refresh_token"));
        }

        var tokens = _jwtTokenService.GenerateTokens(user);
        var isAdmin = await _adminAuthorizationService.IsAdminAsync(user.Email, cancellationToken);

        return Ok(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            new UserInfo(user.Id, user.Email, user.DisplayName, isAdmin)));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleSignIn([FromBody] GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken);
        if (googleUser is null)
        {
            return Unauthorized(new Envelope("Invalid Google token.", "invalid_google_token"));
        }

        // 1. Look up by external provider ID (returning user)
        var user = await _userRepository.GetByExternalProviderAsync("Google", googleUser.Subject, cancellationToken);

        // 2. If not found, try to link by email (existing local account)
        if (user is null)
        {
            user = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);
            if (user is not null)
            {
                user.LinkExternalProvider("Google", googleUser.Subject);
                await _userRepository.SaveChangesAsync(cancellationToken);
            }
        }

        // 3. If still not found, create new user
        if (user is null)
        {
            user = Domain.Entities.User.CreateExternal(googleUser.Email, googleUser.Name, "Google", googleUser.Subject);
            await _userRepository.AddAsync(user, cancellationToken);
        }

        var tokens = _jwtTokenService.GenerateTokens(user);
        var isAdmin = await _adminAuthorizationService.IsAdminAsync(user.Email, cancellationToken);

        return Ok(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            new UserInfo(user.Id, user.Email, user.DisplayName, isAdmin)));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var displayName = User.FindFirst(ClaimTypes.Name)?.Value;

        if (userId is null || email is null)
        {
            return Unauthorized();
        }

        var isAdmin = await _adminAuthorizationService.IsAdminAsync(email, cancellationToken);
        var subscription = await _subscriptionRepository.GetActiveByUserIdAsync(Guid.Parse(userId), cancellationToken);
        SubscriptionTier? subscriptionTier = subscription is not null && !subscription.IsExpired(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            ? subscription.Tier == SubscriptionTier.Free ? SubscriptionTier.Beginner : subscription.Tier
            : null;

        return Ok(new MeResponse(Guid.Parse(userId), email, displayName ?? email, isAdmin, subscriptionTier));
    }

    private static bool IsPasswordComplex(string password)
    {
        if (password.Length < 8) return false;
        if (!password.Any(char.IsUpper)) return false;
        if (!password.Any(char.IsDigit)) return false;
        if (!password.Any(c => !char.IsLetterOrDigit(c))) return false;
        return true;
    }
}

public sealed record RegisterRequest(string Email, string DisplayName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record GoogleAuthRequest(string IdToken);
public sealed record AuthResponse(string Token, string RefreshToken, UserInfo User);
public sealed record UserInfo(Guid Id, string Email, string DisplayName, bool IsAdmin);
public sealed record MeResponse(Guid Id, string Email, string DisplayName, bool IsAdmin, SubscriptionTier? SubscriptionTier);
