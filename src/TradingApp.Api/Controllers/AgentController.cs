using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingApp.Application.Abstractions.Configuration;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.Agent.Services;
using TradingApp.Domain.Entities;
using TradingApp.Persistence;

namespace TradingApp.Api.Controllers;

/// <summary>
/// Endpoints called by the Worker (execution agent) during check-in.
/// The Worker polls POST /heartbeat periodically and receives any pending command.
/// </summary>
[ApiController]
[Route("api/agent")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AgentController : ControllerBase
{
    private static readonly JsonSerializerOptions DataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AgentCommandStore _store;
    private readonly AgentUpdateOptions _updateOptions;
    private readonly HyperliquidOptions _hyperliquidOptions;
    private readonly TradingAppDbContext _db;
    private readonly ISignalRPublisher _signalRPublisher;

    public AgentController(
        AgentCommandStore store,
        IOptions<AgentUpdateOptions> updateOptions,
        IOptions<HyperliquidOptions> hyperliquidOptions,
        TradingAppDbContext db,
        ISignalRPublisher signalRPublisher)
    {
        _store = store;
        _updateOptions = updateOptions.Value;
        _hyperliquidOptions = hyperliquidOptions.Value;
        _db = db;
        _signalRPublisher = signalRPublisher;
    }

    /// <summary>
    /// Agent heartbeat. Worker posts its state; API returns any pending command.
    /// </summary>
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
            });
        }

        var pendingCommands = _store.DrainCommands(heartbeat.AgentId);

        var updateAvailable = !string.IsNullOrEmpty(_updateOptions.LatestVersion) &&
            !string.IsNullOrEmpty(heartbeat.AgentVersion) &&
            IsNewerVersion(_updateOptions.LatestVersion, heartbeat.AgentVersion);

        return Ok(new HeartbeatResponse
        {
            PendingCommands = pendingCommands,
            UpdateAvailable = updateAvailable,
            LatestVersion = updateAvailable ? _updateOptions.LatestVersion : null,
            UpdateDownloadUrl = updateAvailable ? _updateOptions.DownloadUrl : null,
            UpdateSha256Hash = updateAvailable ? _updateOptions.Sha256Hash : null,
            NetworkConfig = BuildNetworkConfig(),
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
    [HttpGet("update/latest")]
    [ProducesResponseType(typeof(AgentUpdateInfo), StatusCodes.Status200OK)]
    public IActionResult GetLatestUpdate()
    {
        return Ok(new AgentUpdateInfo
        {
            Version = _updateOptions.LatestVersion,
            DownloadUrl = _updateOptions.DownloadUrl,
            Sha256Hash = _updateOptions.Sha256Hash,
            ReleaseNotes = _updateOptions.ReleaseNotes,
        });
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

    private NetworkConfig BuildNetworkConfig() => new()
    {
        BaseUrl = _hyperliquidOptions.BaseUrl,
        WsBaseUrl = _hyperliquidOptions.WsBaseUrl,
        Network = _hyperliquidOptions.Network,
    };
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
