using System.Collections.Concurrent;
using TradingApp.Application.Agent.Models;

namespace TradingApp.Application.Agent.Services;

/// <summary>
/// In-memory store that tracks connected agents, their heartbeats,
/// and queued commands. Singleton in the API process.
/// POC implementation — production would persist to DB.
/// </summary>
public sealed class AgentCommandStore
{
    private static readonly TimeSpan DisconnectedThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CommandExpiry = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<AgentCommand>> _pendingCommands = new();

    /// <summary>
    /// Queue a command for a specific agent.
    /// Multiple commands can be queued between heartbeats.
    /// </summary>
    public void EnqueueCommand(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var queue = _pendingCommands.GetOrAdd(command.AgentId, _ => new ConcurrentQueue<AgentCommand>());
        queue.Enqueue(command);
    }

    /// <summary>
    /// Drain all pending commands for an agent (called when the agent checks in).
    /// Expired commands (older than <see cref="CommandExpiry"/>) are discarded.
    /// Returns an empty list if no commands are pending.
    /// </summary>
    public IReadOnlyList<AgentCommand> DrainCommands(string agentId)
    {
        if (!_pendingCommands.TryGetValue(agentId, out var queue))
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var commands = new List<AgentCommand>();
        while (queue.TryDequeue(out var command))
        {
            if (now - command.CreatedAtUtc < CommandExpiry)
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    /// <summary>
    /// Peek at the pending command queue for an agent without draining.
    /// Expired commands are removed during the peek.
    /// </summary>
    public IReadOnlyList<AgentCommand> GetPendingCommands(string agentId)
    {
        if (!_pendingCommands.TryGetValue(agentId, out var queue))
        {
            return [];
        }

        // Snapshot the queue — expired items are cleaned on drain
        var now = DateTimeOffset.UtcNow;
        return queue
            .Where(c => now - c.CreatedAtUtc < CommandExpiry)
            .ToList();
    }

    /// <summary>
    /// Process a heartbeat from an agent: update or register the agent.
    /// </summary>
    public void ProcessHeartbeat(AgentHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        _agents.AddOrUpdate(
            heartbeat.AgentId,
            _ => new AgentInfo
            {
                AgentId = heartbeat.AgentId,
                MachineName = heartbeat.MachineName,
                State = heartbeat.State,
                LastHeartbeat = heartbeat.TimestampUtc,
                ConnectedSince = heartbeat.TimestampUtc,
                WalletAddress = heartbeat.WalletAddress,
                ActiveStrategy = heartbeat.ActiveStrategy,
                LastError = heartbeat.LastError,
                AgentVersion = heartbeat.AgentVersion,
                UpdateState = heartbeat.UpdateState,
            },
            (_, existing) =>
            {
                // Never let a heartbeat override a killed agent
                if (existing.KilledAtUtc.HasValue &&
                    DateTimeOffset.UtcNow >= existing.KilledAtUtc.Value)
                {
                    existing.LastHeartbeat = heartbeat.TimestampUtc;
                    return existing;
                }

                existing.State = heartbeat.State;
                existing.LastHeartbeat = heartbeat.TimestampUtc;
                existing.WalletAddress = heartbeat.WalletAddress;
                existing.ActiveStrategy = heartbeat.ActiveStrategy;
                existing.LastError = heartbeat.LastError;
                existing.AgentVersion = heartbeat.AgentVersion;
                existing.UpdateState = heartbeat.UpdateState;
                return existing;
            });
    }

    /// <summary>
    /// Get all known agents, evaluating effective state (disconnected/killed).
    /// </summary>
    public IReadOnlyList<AgentInfo> GetAllAgents()
    {
        var agents = _agents.Values.ToList();

        foreach (var agent in agents)
        {
            EvaluateEffectiveState(agent);
        }

        return agents;
    }

    /// <summary>
    /// Get a specific agent by ID.
    /// </summary>
    public AgentInfo? GetAgent(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return null;
        }

        EvaluateEffectiveState(agent);
        return agent;
    }

    /// <summary>
    /// Check whether an agent is currently killed (effective now).
    /// Also checks if any other agent with the same wallet address is killed.
    /// Returns the reason if killed, null otherwise.
    /// </summary>
    public string? GetKillReason(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            if (agent.KilledAtUtc.HasValue && DateTimeOffset.UtcNow >= agent.KilledAtUtc.Value)
            {
                return agent.KilledReason ?? "Agent access revoked.";
            }

            // Check if another agent entry with the same wallet is killed
            if (!string.IsNullOrEmpty(agent.WalletAddress))
            {
                var walletKill = FindKilledWallet(agent.WalletAddress, agentId);
                if (walletKill is not null)
                {
                    return walletKill;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Check if any agent (other than <paramref name="excludeAgentId"/>) with the given wallet is killed.
    /// </summary>
    private string? FindKilledWallet(string walletAddress, string excludeAgentId)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var other in _agents.Values)
        {
            if (string.Equals(other.AgentId, excludeAgentId, StringComparison.Ordinal))
                continue;

            if (string.Equals(other.WalletAddress, walletAddress, StringComparison.OrdinalIgnoreCase) &&
                other.KilledAtUtc.HasValue &&
                now >= other.KilledAtUtc.Value)
            {
                return other.KilledReason ?? "Wallet access revoked.";
            }
        }

        return null;
    }

    /// <summary>
    /// Kill an agent immediately or schedule a kill at a future time.
    /// The agent will be forced to stop and rejected on heartbeat.
    /// </summary>
    public bool KillAgent(string agentId, string? reason, DateTimeOffset? effectiveAtUtc)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return false;
        }

        agent.KilledAtUtc = effectiveAtUtc ?? DateTimeOffset.UtcNow;
        agent.KilledReason = reason;
        EvaluateEffectiveState(agent);
        return true;
    }

    /// <summary>
    /// Reinstate a killed agent, allowing it to reconnect.
    /// </summary>
    public bool ReinstateAgent(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return false;
        }

        agent.KilledAtUtc = null;
        agent.KilledReason = null;

        // Reset state so the agent can reconnect on next heartbeat
        if (agent.State == AgentState.Killed)
        {
            agent.State = AgentState.Disconnected;
        }

        return true;
    }

    private void EvaluateEffectiveState(AgentInfo agent)
    {
        var now = DateTimeOffset.UtcNow;

        // Kill switch takes priority over everything
        if (agent.KilledAtUtc.HasValue && now >= agent.KilledAtUtc.Value)
        {
            agent.State = AgentState.Killed;
            return;
        }

        // Disconnected detection
        if (now - agent.LastHeartbeat > DisconnectedThreshold &&
            agent.State is not (AgentState.Disconnected or AgentState.Killed))
        {
            agent.State = AgentState.Disconnected;
        }
    }
}
