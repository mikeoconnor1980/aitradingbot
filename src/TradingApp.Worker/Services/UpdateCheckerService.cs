using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TradingApp.Application.Agent.Models;

namespace TradingApp.Worker.Services;

/// <summary>
/// Background service that downloads and applies agent updates.
/// Receives update notifications from <see cref="AgentCheckInService"/> via <see cref="IUpdateNotifier"/>,
/// checks whether it's safe to update (no active trading sessions), and launches the
/// Inno Setup installer in silent mode.
/// </summary>
public sealed class UpdateCheckerService : BackgroundService, IUpdateNotifier
{
    private static readonly TimeSpan SafeToUpdateRecheck = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxDeferralDuration = TimeSpan.FromHours(4);

    private readonly ITradingHealthProvider _healthProvider;
    private readonly ILogger<UpdateCheckerService> _logger;
    private readonly SemaphoreSlim _updateSignal = new(0, 1);

    private volatile UpdateInfo? _pendingUpdate;

    public UpdateCheckerService(
        ITradingHealthProvider healthProvider,
        ILogger<UpdateCheckerService> logger)
    {
        _healthProvider = healthProvider;
        _logger = logger;
    }

    /// <summary>Current update state reported back via heartbeat.</summary>
    public UpdateState CurrentState { get; private set; } = UpdateState.None;

    /// <summary>Reason if update is deferred (e.g. active trading session).</summary>
    public string? DeferredReason { get; private set; }

    public void NotifyUpdateAvailable(string version, string downloadUrl, string sha256Hash)
    {
        var existing = _pendingUpdate;
        if (existing is not null && existing.Version == version)
            return; // Already aware of this version

        _pendingUpdate = new UpdateInfo(version, downloadUrl, sha256Hash);
        _logger.LogInformation("Update available: v{Version} from {Url}", version, downloadUrl);

        // Signal the background loop to wake up
        if (_updateSignal.CurrentCount == 0)
            _updateSignal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UpdateCheckerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a notification or periodic wake (60 min)
                await _updateSignal.WaitAsync(TimeSpan.FromMinutes(60), stoppingToken);

                var update = _pendingUpdate;
                if (update is null)
                    continue;

                await ProcessUpdateAsync(update, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateCheckerService encountered an error.");
                CurrentState = UpdateState.Failed;
                DeferredReason = null;

                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("UpdateCheckerService stopped.");
    }

    private async Task ProcessUpdateAsync(UpdateInfo update, CancellationToken stoppingToken)
    {
        // --- SafeToUpdate check ---
        var deferralStart = DateTimeOffset.UtcNow;

        while (!IsSafeToUpdate())
        {
            var elapsed = DateTimeOffset.UtcNow - deferralStart;

            if (elapsed >= MaxDeferralDuration)
            {
                _logger.LogWarning(
                    "Update to v{Version} deferred for {Hours:F1}h (max deferral exceeded). " +
                    "Operator intervention required — use the kill switch to stop trading before updating.",
                    update.Version, elapsed.TotalHours);
                CurrentState = UpdateState.Deferred;
                DeferredReason = $"Max deferral exceeded ({MaxDeferralDuration.TotalHours}h). Operator action required.";
                return;
            }

            CurrentState = UpdateState.Deferred;
            DeferredReason = "Active trading session — waiting for session to end.";

            _logger.LogInformation(
                "Update to v{Version} deferred: active trading session. " +
                "Re-checking in {Minutes} min (elapsed: {Elapsed:F0} min, max: {Max:F0} min).",
                update.Version,
                SafeToUpdateRecheck.TotalMinutes,
                elapsed.TotalMinutes,
                MaxDeferralDuration.TotalMinutes);

            await Task.Delay(SafeToUpdateRecheck, stoppingToken);
        }

        CurrentState = UpdateState.Downloading;
        DeferredReason = null;

        // --- Download ---
        var tempDir = Path.Combine(Path.GetTempPath(), "TradingApp-Update");
        Directory.CreateDirectory(tempDir);

        var fileName = $"TradingApp-ExecutionAgent-v{update.Version}-Setup.exe";
        var filePath = Path.Combine(tempDir, fileName);

        _logger.LogInformation("Downloading update v{Version} to {Path}...", update.Version, filePath);

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update v{Version} from {Url}.", update.Version, update.DownloadUrl);
            CurrentState = UpdateState.Failed;
            return;
        }

        // --- Verify SHA256 hash ---
        _logger.LogInformation("Verifying SHA256 hash...");
        var actualHash = await ComputeSha256Async(filePath, stoppingToken);

        if (!string.Equals(actualHash, update.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "SHA256 hash mismatch for update v{Version}. Expected={Expected}, Actual={Actual}. " +
                "Update REJECTED — the downloaded file may be corrupt or tampered with.",
                update.Version, update.Sha256Hash, actualHash);

            CurrentState = UpdateState.Failed;
            DeferredReason = null;

            // Clean up the suspect file
            try { File.Delete(filePath); } catch { /* best effort */ }
            return;
        }

        _logger.LogInformation("SHA256 verified. Applying update v{Version}...", update.Version);

        // --- Apply update (launch silent installer) ---
        CurrentState = UpdateState.Applying;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG",
                    UseShellExecute = true,
                    Verb = "runas", // Elevate (required for service management)
                },
            };

            process.Start();

            _logger.LogInformation(
                "Installer launched (PID={Pid}). The service will be stopped and restarted by the installer. " +
                "This process will terminate shortly.",
                process.Id);

            // The Inno Setup installer will stop our service, replace files, and restart it.
            // Our process will be killed externally — this is expected behavior.
            // We don't await the process; the service manager handles the lifecycle.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch installer for update v{Version}.", update.Version);
            CurrentState = UpdateState.Failed;
        }
    }

    private bool IsSafeToUpdate()
    {
        // Safe if the agent is idle (no WebSocket connection implies no active trading)
        // The health provider tracks whether there's an active WebSocket connection
        // and recent trade activity. If no trades are flowing, it's safe.
        var snapshot = _healthProvider.GetSnapshot();

        // Not safe if WebSocket is connected and we've received trades recently (active session)
        if (snapshot.IsWebSocketConnected && snapshot.TimeSinceLastTrade is { TotalMinutes: < 5 })
        {
            return false;
        }

        return true;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private sealed record UpdateInfo(string Version, string DownloadUrl, string Sha256Hash);
}
