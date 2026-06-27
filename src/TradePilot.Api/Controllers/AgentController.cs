using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradePilot.Application.Abstractions.Configuration;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Agent;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Serialization;
using TradePilot.Application.Trading;
using TradePilot.Domain.Entities;
using TradePilot.Persistence;

namespace TradePilot.Api.Controllers;

/// <summary>
/// Endpoints called by the Worker (execution agent) during check-in.
/// The Worker polls POST /heartbeat periodically and receives any pending command.
/// </summary>
[ApiController]
[Route("api/agent")]
[Produces("application/json")]
[Authorize]
public sealed class AgentController : ControllerBase
{
    private static readonly JsonSerializerOptions DataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly TimeSpan WorkerDownloadTokenLifetime = TimeSpan.FromMinutes(10);
    private const string WorkerDownloadPurpose = "agent-worker-download";

    private readonly ILogger<AgentController> _logger;
    private readonly AgentCommandStore _store;
    private readonly AgentUpdateOptions _updateOptions;
    private readonly IInstallerStore _installerStore;
    private readonly HyperliquidOptions _hyperliquidOptions;
    private readonly TelegramOptions _telegramOptions;
    private readonly TradePilotDbContext _db;
    private readonly ISignalRPublisher _signalRPublisher;
    private readonly IStrategyRepository _strategyRepository;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;
    private readonly IDataProtector _workerDownloadProtector;

    public AgentController(
        ILogger<AgentController> logger,
        AgentCommandStore store,
        IOptions<AgentUpdateOptions> updateOptions,
        IInstallerStore installerStore,
        IOptions<HyperliquidOptions> hyperliquidOptions,
        IOptions<TelegramOptions> telegramOptions,
        TradePilotDbContext db,
        ISignalRPublisher signalRPublisher,
        IStrategyRepository strategyRepository,
        ISubscriptionFeatureService subscriptionFeatureService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _logger = logger;
        _store = store;
        _updateOptions = updateOptions.Value;
        _installerStore = installerStore;
        _hyperliquidOptions = hyperliquidOptions.Value;
        _telegramOptions = telegramOptions.Value;
        _db = db;
        _signalRPublisher = signalRPublisher;
        _strategyRepository = strategyRepository;
        _subscriptionFeatureService = subscriptionFeatureService;
        _workerDownloadProtector = dataProtectionProvider
            .CreateProtector(WorkerDownloadPurpose);
    }

    /// <summary>
    /// Agent heartbeat. Worker posts its state; API returns any pending command.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeat heartbeat)
    {
        if (string.IsNullOrWhiteSpace(heartbeat.AgentId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid heartbeat",
                Detail = "AgentId is required."
            });
        }

        _store.ProcessHeartbeat(heartbeat);

        // Persist and broadcast execution logs
        if (heartbeat.ExecutionLogs is { Count: > 0 })
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in heartbeat.ExecutionLogs)
            {
                _db.ExecutionLogs.Add(new ExecutionLog
                {
                    Id = Guid.NewGuid(),
                    AgentId = heartbeat.AgentId,
                    TimestampUtc = entry.TimestampUtc,
                    Category = entry.Category.ToString(),
                    Level = entry.Level.ToString(),
                    Message = entry.Message,
                    Data = entry.Data is not null ? JsonSerializer.Serialize(entry.Data, DataJsonOptions) : null,
                    ReceivedAtUtc = now,
                });

                await _signalRPublisher.BroadcastExecutionLogAsync(new ExecutionLogDto
                {
                    AgentId = heartbeat.AgentId,
                    TimestampUtc = entry.TimestampUtc,
                    Category = entry.Category.ToString(),
                    Level = entry.Level.ToString(),
                    Message = entry.Message,
                    Data = entry.Data,
                });
            }

            await _db.SaveChangesAsync();
        }

        // Check kill switch — if killed, tell the agent to shut down
        var killReason = _store.GetKillReason(heartbeat.AgentId);
        if (killReason is not null)
        {
            return Ok(new HeartbeatResponse
            {
                PendingCommands = [],
                MustShutdown = true,
                ShutdownReason = killReason,
                NetworkConfig = BuildNetworkConfig(),
                NotificationConfig = await BuildNotificationConfigAsync(heartbeat.WalletAddress),
            });
        }

        await EnqueueAutoResumeCommandIfNeededAsync(heartbeat);

        var pendingCommands = _store.DrainCommands(heartbeat.AgentId);

        var latestRelease = await ResolveInstallerReleaseAsync(cancellationToken: HttpContext.RequestAborted);
        var updateAvailable = latestRelease.ExeAvailable &&
            !string.IsNullOrEmpty(latestRelease.Version) &&
            !string.IsNullOrEmpty(heartbeat.AgentVersion) &&
            IsNewerVersion(latestRelease.Version, heartbeat.AgentVersion);

        if (updateAvailable)
        {
            _logger.LogInformation(
                "Update available for agent {AgentId}: currentVersion={CurrentVersion}, latestVersion={LatestVersion}, status={Status}, blob={BlobName}",
                heartbeat.AgentId,
                heartbeat.AgentVersion,
                latestRelease.Version,
                latestRelease.Status,
                latestRelease.ExeFile?.BlobName ?? latestRelease.ExeFile?.FileName ?? string.Empty);
        }

        return Ok(new HeartbeatResponse
        {
            PendingCommands = pendingCommands,
            UpdateAvailable = updateAvailable,
            LatestVersion = updateAvailable ? latestRelease.Version : null,
            UpdateDownloadUrl = updateAvailable ? GetWorkerUpdateDownloadUrl(latestRelease) : null,
            UpdateSha256Hash = updateAvailable ? latestRelease.ExeSha256Hash : null,
            NetworkConfig = BuildNetworkConfig(),
            NotificationConfig = await BuildNotificationConfigAsync(heartbeat.WalletAddress),
        });
    }

    /// <summary>
    /// List all connected agents (called by the dashboard).
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentInfo>), StatusCodes.Status200OK)]
    public IActionResult ListAgents()
    {
        return Ok(_store.GetAllAgents());
    }

    /// <summary>
    /// Relay a real-time event from the Worker agent to connected SignalR clients.
    /// Used when the Worker doesn't have a direct Azure SignalR connection.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("relay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Relay([FromBody] SignalRRelayEnvelope envelope)
    {
        if (envelope.Fill is { } fill)
        {
            await _signalRPublisher.BroadcastFillEventAsync(fill);
        }

        if (envelope.OrderUpdate is { } orderUpdate)
        {
            await _signalRPublisher.BroadcastOrderUpdateAsync(orderUpdate);
        }

        if (envelope.UserConnectionStatus is { } status)
        {
            await _signalRPublisher.BroadcastUserConnectionStatusAsync(status);
        }

        return NoContent();
    }

    /// <summary>
    /// Get a specific agent's details (called by the dashboard).
    /// </summary>
    [HttpGet("{agentId}")]
    [ProducesResponseType(typeof(AgentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAgent(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound();
        }

        return Ok(agent);
    }

    /// <summary>
    /// Get pending commands queued for a specific agent.
    /// </summary>
    [HttpGet("{agentId}/pending-commands")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingCommandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPendingCommands(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound();
        }

        var commands = _store.GetPendingCommands(agentId)
            .Select(c => new PendingCommandDto
            {
                CommandId = c.CommandId,
                Type = c.Type,
                CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToList();

        return Ok(commands);
    }

    /// <summary>
    /// Get execution log entries for a specific agent.
    /// </summary>
    [HttpGet("{agentId}/execution-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<ExecutionLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutionLogs(
        string agentId,
        [FromQuery] DateTimeOffset? since = null,
        [FromQuery] int limit = 200,
        [FromQuery] string? level = null)
    {
        limit = Math.Clamp(limit, 1, 1000);

        var query = _db.ExecutionLogs
            .Where(e => e.AgentId == agentId)
            .AsNoTracking();

        if (since.HasValue)
        {
            query = query.Where(e => e.TimestampUtc >= since.Value);
        }

        if (!string.IsNullOrEmpty(level))
        {
            query = query.Where(e => e.Level == level);
        }

        var rows = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .Select(e => new { e.AgentId, e.TimestampUtc, e.Category, e.Level, e.Message, e.Data })
            .ToListAsync();

        var logs = rows.Select(e => new ExecutionLogDto
        {
            AgentId = e.AgentId,
            TimestampUtc = e.TimestampUtc,
            Category = e.Category,
            Level = e.Level,
            Message = e.Message,
            Data = e.Data != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(e.Data, DataJsonOptions) : null,
        }).ToList();

        return Ok(logs);
    }

    /// <summary>
    /// Kill an agent. Forces shutdown and prevents reconnection until reinstated.
    /// Optionally schedule the kill at a future date/time.
    /// </summary>
    [HttpPost("{agentId}/kill")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Kill(string agentId, [FromBody] KillAgentRequest request)
    {
        if (!_store.KillAgent(agentId, request.Reason, request.EffectiveAtUtc))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'."
            });
        }

        return Ok(new { message = $"Agent '{agentId}' kill switch activated." });
    }

    /// <summary>
    /// Reinstate a killed agent, allowing it to reconnect.
    /// </summary>
    [HttpPost("{agentId}/reinstate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Reinstate(string agentId)
    {
        if (!_store.ReinstateAgent(agentId))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'."
            });
        }

        return Ok(new { message = $"Agent '{agentId}' reinstated. It may reconnect." });
    }

    /// <summary>
    /// Get the latest agent version metadata. Used by agents as a fallback
    /// update check and by operators for manual verification.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("update/latest")]
    [ProducesResponseType(typeof(AgentUpdateInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestUpdate(CancellationToken cancellationToken)
    {
        var latestRelease = await ResolveInstallerReleaseAsync(cancellationToken);

        return Ok(new AgentUpdateInfo
        {
            Version = latestRelease.Version,
            DownloadUrl = latestRelease.ExeAvailable ? GetWorkerUpdateDownloadUrl(latestRelease) : string.Empty,
            Sha256Hash = latestRelease.ExeAvailable ? latestRelease.ExeSha256Hash ?? string.Empty : string.Empty,
            ReleaseNotes = latestRelease.ReleaseNotes,
        });
    }

    /// <summary>
    /// Download the installer binary. Streams the file from blob storage or local disk.
    /// </summary>
    [HttpGet("installer/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInstaller([FromQuery] string format = "exe", CancellationToken cancellationToken = default)
    {
        await EnsureInstallerDownloadAccessAsync(cancellationToken);
        return await DownloadInstallerInternalAsync(format, cancellationToken);
    }

    /// <summary>
    /// Download the installer binary using a short-lived worker token issued during heartbeat.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("installer/worker-download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInstallerForWorker(
        [FromQuery] string format = "exe",
        [FromQuery] string? token = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Missing download token",
                Detail = "A short-lived worker download token is required."
            });
        }

        if (!TryValidateWorkerDownloadToken(token, format, out var tokenValidationError))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid download token",
                Detail = tokenValidationError
            });
        }

        return await DownloadInstallerInternalAsync(format, cancellationToken);
    }

    private async Task<IActionResult> DownloadInstallerInternalAsync(string format, CancellationToken cancellationToken)
    {
        var normalizedFormat = format.ToLowerInvariant();
        if (normalizedFormat is not "exe" and not "zip")
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid format",
                Detail = $"Format '{format}' is not supported. Use 'exe' or 'zip'."
            });
        }

        var latestRelease = await ResolveInstallerReleaseAsync(cancellationToken);
        var releaseFile = normalizedFormat == "zip"
            ? latestRelease.ZipFile
            : latestRelease.ExeFile;
        var fileAvailable = normalizedFormat == "zip"
            ? latestRelease.ZipAvailable
            : latestRelease.ExeAvailable;

        if (releaseFile is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Installer not available",
                Detail = $"No installer metadata is available for format '{normalizedFormat}'."
            });
        }

        if (!fileAvailable)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Installer not found",
                Detail = $"The installer file '{releaseFile.FileName}' was not found on the server."
            });
        }

        var stream = await _installerStore.OpenReadAsync(releaseFile, cancellationToken);
        if (stream is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Installer not found",
                Detail = $"The installer file '{releaseFile.FileName}' was not found on the server."
            });
        }

        _logger.LogInformation("Installer download: format={Format}, version={Version}, file={FileName}",
            normalizedFormat, latestRelease.Version, releaseFile.FileName);

        return File(stream, releaseFile.ContentType, releaseFile.FileName);
    }

    /// <summary>
    /// Get installer metadata (version, availability, file sizes, hash).
    /// Used by the web UI to render download buttons.
    /// </summary>
    [HttpGet("installer/info")]
    [ProducesResponseType(typeof(InstallerInfoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstallerInfo(CancellationToken cancellationToken)
    {
        var latestRelease = await ResolveInstallerReleaseAsync(cancellationToken);

        return Ok(new InstallerInfoResponse
        {
            Status = latestRelease.Status,
            Version = latestRelease.Version,
            ExeAvailable = latestRelease.ExeAvailable,
            ZipAvailable = latestRelease.ZipAvailable,
            ExeFileName = latestRelease.ExeFile?.FileName,
            ZipFileName = latestRelease.ZipFile?.FileName,
            ExeFileSizeBytes = latestRelease.ExeFileSizeBytes,
            ZipFileSizeBytes = latestRelease.ZipFileSizeBytes,
            Sha256Hash = latestRelease.ExeSha256Hash ?? string.Empty,
            ExeSha256Hash = latestRelease.ExeSha256Hash,
            ZipSha256Hash = latestRelease.ZipSha256Hash,
            PublishedAtUtc = latestRelease.PublishedAtUtc,
            MinimumSupportedVersion = latestRelease.MinimumSupportedVersion,
            ReleaseNotes = latestRelease.ReleaseNotes,
        });
    }

    private async Task<InstallerReleaseResolution> ResolveInstallerReleaseAsync(CancellationToken cancellationToken)
    {
        var manifest = await _installerStore.GetLatestReleaseManifestAsync(cancellationToken);
        if (manifest is not null)
        {
            return await BuildManifestReleaseAsync(manifest, cancellationToken);
        }

        return await BuildFallbackReleaseAsync(cancellationToken);
    }

    private async Task<InstallerReleaseResolution> BuildManifestReleaseAsync(
        InstallerReleaseManifest manifest,
        CancellationToken cancellationToken)
    {
        manifest.Files.TryGetValue("exe", out var exeFile);
        manifest.Files.TryGetValue("zip", out var zipFile);

        var exeInfoTask = GetReleaseFileInfoAsync(exeFile, cancellationToken);
        var zipInfoTask = GetReleaseFileInfoAsync(zipFile, cancellationToken);
        await Task.WhenAll(exeInfoTask, zipInfoTask);

        var exeInfo = exeInfoTask.Result;
        var zipInfo = zipInfoTask.Result;
        var status = exeFile is null || zipFile is null || !exeInfo.Exists || !zipInfo.Exists
            ? "ManifestFoundBlobMissing"
            : "Available";

        if (status == "ManifestFoundBlobMissing")
        {
            var missingArtifacts = new List<string>();
            if (exeFile is null)
            {
                missingArtifacts.Add("exe:metadata-missing");
            }
            else if (!exeInfo.Exists)
            {
                missingArtifacts.Add($"exe:{exeFile.BlobName ?? exeFile.FileName}");
            }

            if (zipFile is null)
            {
                missingArtifacts.Add("zip:metadata-missing");
            }
            else if (!zipInfo.Exists)
            {
                missingArtifacts.Add($"zip:{zipFile.BlobName ?? zipFile.FileName}");
            }

            _logger.LogWarning(
                "Installer manifest v{Version} resolved with missing artifacts: {MissingArtifacts}",
                manifest.Version,
                string.Join(", ", missingArtifacts));
        }
        else
        {
            _logger.LogDebug(
                "Installer manifest v{Version} resolved successfully. ExeBlob={ExeBlobName}, ZipBlob={ZipBlobName}",
                manifest.Version,
                exeFile?.BlobName ?? exeFile?.FileName ?? string.Empty,
                zipFile?.BlobName ?? zipFile?.FileName ?? string.Empty);
        }

        return new InstallerReleaseResolution(
            status,
            manifest.Version,
            manifest.ReleaseNotes,
            manifest.PublishedAtUtc,
            manifest.MinimumSupportedVersion,
            exeFile,
            exeInfo.Exists,
            exeInfo.SizeBytes,
            zipFile,
            zipInfo.Exists,
            zipInfo.SizeBytes,
            false);
    }

    private async Task<InstallerReleaseResolution> BuildFallbackReleaseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_updateOptions.LatestVersion))
        {
            return InstallerReleaseResolution.NoManifest();
        }

        var version = _updateOptions.LatestVersion;
        var exeFile = new InstallerReleaseFile
        {
            FileName = $"TradePilot-ExecutionAgent-v{version}-Setup.exe",
            ContentType = "application/octet-stream",
            Sha256 = _updateOptions.Sha256Hash,
        };
        var zipFile = new InstallerReleaseFile
        {
            FileName = $"TradePilot-ExecutionAgent-v{version}-win-x64.zip",
            ContentType = "application/zip",
        };

        var exeTask = _installerStore.GetFileInfoAsync(exeFile.FileName, cancellationToken);
        var zipTask = _installerStore.GetFileInfoAsync(zipFile.FileName, cancellationToken);
        await Task.WhenAll(exeTask, zipTask);

        var exeInfo = exeTask.Result;
        var zipInfo = zipTask.Result;

        _logger.LogInformation(
            "Installer manifest not found. Using fallback update configuration for version {Version}.",
            version);

        return new InstallerReleaseResolution(
            "FallbackConfigured",
            version,
            _updateOptions.ReleaseNotes,
            null,
            _updateOptions.MinimumVersion,
            exeFile,
            exeInfo.Exists,
            exeInfo.SizeBytes,
            zipFile,
            zipInfo.Exists,
            zipInfo.SizeBytes,
            true);
    }

    private Task<InstallerFileInfo> GetReleaseFileInfoAsync(
        InstallerReleaseFile? releaseFile,
        CancellationToken cancellationToken)
    {
        if (releaseFile is null)
        {
            return Task.FromResult(new InstallerFileInfo(false, null));
        }

        var fileName = !string.IsNullOrWhiteSpace(releaseFile.BlobName)
            ? releaseFile.BlobName
            : releaseFile.FileName;

        return _installerStore.GetFileInfoAsync(fileName, cancellationToken);
    }

    private string GetWorkerUpdateDownloadUrl(InstallerReleaseResolution latestRelease)
    {
        if (latestRelease.UsesFallbackConfiguration && !string.IsNullOrWhiteSpace(_updateOptions.DownloadUrl))
        {
            return ResolveAbsoluteDownloadUrl(_updateOptions.DownloadUrl);
        }

        var token = CreateWorkerDownloadToken("exe");
        var actionUrl = Url.Action(
            action: nameof(DownloadInstallerForWorker),
            controller: null,
            values: new { format = "exe", token },
            protocol: Request.Scheme,
            host: Request.Host.ToUriComponent());

        return ResolveAbsoluteDownloadUrl(actionUrl)
            .OrIfEmpty(ResolveAbsoluteDownloadUrl(_updateOptions.DownloadUrl));
    }

    private string CreateWorkerDownloadToken(string format)
    {
        var normalizedFormat = format.ToLowerInvariant();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(WorkerDownloadTokenLifetime).ToUnixTimeMilliseconds();
        var payload = $"{normalizedFormat}|{expiresAtUtc}";
        return _workerDownloadProtector.Protect(payload);
    }

    private bool TryValidateWorkerDownloadToken(string token, string format, out string error)
    {
        try
        {
            var payload = _workerDownloadProtector.Unprotect(token);
            var parts = payload.Split('|', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                error = "The token payload is malformed.";
                return false;
            }

            if (!string.Equals(parts[0], format, StringComparison.OrdinalIgnoreCase))
            {
                error = "The token format does not match the requested installer format.";
                return false;
            }

            if (!long.TryParse(parts[1], out var expiresAtUtcMs))
            {
                error = "The token expiry is malformed.";
                return false;
            }

            if (DateTimeOffset.UtcNow > DateTimeOffset.FromUnixTimeMilliseconds(expiresAtUtcMs))
            {
                error = "The token is expired.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rejected worker installer download token for format {Format}", format);
            error = "The token is invalid or expired.";
            return false;
        }
    }

    private async Task EnsureInstallerDownloadAccessAsync(CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required to download installer packages.");
        }

        var activeTier = await _subscriptionFeatureService.GetActiveTierAsync(userId.Value, cancellationToken);
        if (!activeTier.HasValue)
        {
            throw new UnauthorizedAccessException("An active subscription is required to download installer packages.");
        }
    }

    private Guid? GetAuthenticatedUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && Guid.TryParse(claim, out var userId)
            ? userId
            : null;
    }

    private string ResolveAbsoluteDownloadUrl(string? downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(downloadUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (!Request.Host.HasValue)
        {
            _logger.LogWarning(
                "Unable to make installer download URL absolute because the request host is unavailable. Url={DownloadUrl}",
                downloadUrl);
            return downloadUrl;
        }

        var requestRoot = UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, "/");
        return new Uri(new Uri(requestRoot), downloadUrl).ToString();
    }

    /// <summary>
    /// Compare two semver strings. Returns true if <paramref name="latest"/>
    /// is strictly newer than <paramref name="current"/>.
    /// </summary>
    private static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVersion) &&
            Version.TryParse(current, out var currentVersion))
        {
            return latestVersion > currentVersion;
        }

        return false;
    }

    private async Task EnqueueAutoResumeCommandIfNeededAsync(AgentHeartbeat heartbeat)
    {
        if (heartbeat.State is not AgentState.Idle)
        {
            return;
        }

        var pendingCommands = _store.GetPendingCommands(heartbeat.AgentId);
        if (pendingCommands.Any(command => command.Type == AgentCommandType.Start))
        {
            return;
        }

        var strategy = await _strategyRepository.GetRunningAssignedToAgentAsync(heartbeat.AgentId);
        if (strategy is null)
        {
            return;
        }

        StrategyConfig strategyConfig;
        try
        {
            strategyConfig = JsonSerializer.Deserialize<StrategyConfig>(strategy.ConfigJson, StrategyJsonOptions.Default)
                ?? throw new JsonException("Strategy config deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to auto-resume strategy {StrategyId} for agent {AgentId} because the config is invalid.",
                strategy.Id,
                heartbeat.AgentId);
            return;
        }

        if (!LiveTradingSupport.TryValidate(strategyConfig, out var unsupportedReason))
        {
            _logger.LogWarning(
                "Skipping auto-resume for strategy {StrategyId} on agent {AgentId}: {Reason}",
                strategy.Id,
                heartbeat.AgentId,
                unsupportedReason);
            return;
        }

        _store.EnqueueCommand(new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = heartbeat.AgentId,
            Type = AgentCommandType.Start,
            StrategyId = strategy.Id,
            StrategyConfig = strategyConfig,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        _logger.LogInformation(
            "Queued auto-resume for strategy {StrategyId} on agent {AgentId} after idle heartbeat.",
            strategy.Id,
            heartbeat.AgentId);
    }

    private NetworkConfig BuildNetworkConfig() => new()
    {
        BaseUrl = _hyperliquidOptions.BaseUrl,
        WsBaseUrl = _hyperliquidOptions.WsBaseUrl,
        Network = _hyperliquidOptions.Network,
    };

    private async Task<NotificationConfig?> BuildNotificationConfigAsync(string? walletAddress)
    {
        if (string.IsNullOrWhiteSpace(_telegramOptions.BotToken))
        {
            _logger.LogWarning("Telegram bot token not configured — skipping notification config");
            return null;
        }

        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            _logger.LogWarning("No wallet address in heartbeat — skipping notification config");
            return null;
        }

        // Find a wallet registration whose owner has a linked Telegram chat ID.
        // The same wallet may be registered to multiple users; pick the one with Telegram linked.
        var chatId = await _db.UserWalletAddresses
            .Where(w => w.WalletAddress == walletAddress && w.IsActive)
            .Join(_db.Users, w => w.UserId, u => u.Id, (w, u) => new { u.Id, u.TelegramChatId })
            .Where(x => x.TelegramChatId != null)
            .Select(x => x.TelegramChatId)
            .FirstOrDefaultAsync();

        if (chatId is null)
        {
            // Single-tenant fallback: find any user with a Telegram chat ID linked (POC — only one user).
            chatId = await _db.Users
                .Where(u => u.TelegramChatId != null)
                .Select(u => u.TelegramChatId)
                .FirstOrDefaultAsync();

            if (chatId is null)
            {
                _logger.LogWarning(
                    "No Telegram-linked user found for wallet {WalletAddress} and no fallback user exists",
                    walletAddress);
                return null;
            }

            _logger.LogInformation(
                "No Telegram-linked wallet owner for {WalletAddress} — using single-tenant fallback (ChatId={ChatId})",
                walletAddress, chatId);
        }
        else
        {
            _logger.LogDebug(
                "Notification config built for wallet {WalletAddress}: ChatId={ChatId}",
                walletAddress, chatId);
        }

        return new NotificationConfig
        {
            TelegramChatId = chatId,
            TelegramBotToken = _telegramOptions.BotToken,
        };
    }

    private sealed record InstallerReleaseResolution(
        string Status,
        string Version,
        string? ReleaseNotes,
        DateTimeOffset? PublishedAtUtc,
        string? MinimumSupportedVersion,
        InstallerReleaseFile? ExeFile,
        bool ExeAvailable,
        long? ExeFileSizeBytes,
        InstallerReleaseFile? ZipFile,
        bool ZipAvailable,
        long? ZipFileSizeBytes,
        bool UsesFallbackConfiguration)
    {
        public string? ExeSha256Hash => ExeFile?.Sha256;

        public string? ZipSha256Hash => ZipFile?.Sha256;

        public static InstallerReleaseResolution NoManifest() => new(
            "NoManifest",
            string.Empty,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            false,
            null,
            false);
    }
}

internal static class StringFallbackExtensions
{
    public static string OrIfEmpty(this string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}

public sealed class PendingCommandDto
{
    public required string CommandId { get; init; }
    public required AgentCommandType Type { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class KillAgentRequest
{
    /// <summary>Reason for killing the agent (shown to operator).</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When to kill. Null = immediately. Set to a future UTC date/time
    /// to schedule (e.g. subscription expiry).
    /// </summary>
    public DateTimeOffset? EffectiveAtUtc { get; init; }
}

public sealed class AgentUpdateInfo
{
    public string Version { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string Sha256Hash { get; init; } = string.Empty;
    public string? ReleaseNotes { get; init; }
}

public sealed class InstallerInfoResponse
{
    public string Status { get; init; } = "NoManifest";
    public string Version { get; init; } = string.Empty;
    public bool ExeAvailable { get; init; }
    public bool ZipAvailable { get; init; }
    public string? ExeFileName { get; init; }
    public string? ZipFileName { get; init; }
    public long? ExeFileSizeBytes { get; init; }
    public long? ZipFileSizeBytes { get; init; }
    public string Sha256Hash { get; init; } = string.Empty;
    public string? ExeSha256Hash { get; init; }
    public string? ZipSha256Hash { get; init; }
    public DateTimeOffset? PublishedAtUtc { get; init; }
    public string? MinimumSupportedVersion { get; init; }
    public string? ReleaseNotes { get; init; }
}
