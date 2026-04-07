namespace TradingApp.Worker.Services;

/// <summary>
/// Notifies the update checker that a new version is available.
/// Called by <see cref="AgentCheckInService"/> when the heartbeat response flags an update.
/// </summary>
public interface IUpdateNotifier
{
    void NotifyUpdateAvailable(string version, string downloadUrl, string sha256Hash);
}
