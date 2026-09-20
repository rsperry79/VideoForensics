namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// State snapshot for the update-check service, exposing whether a newer release
    /// is available, its version, the current version, and any errors encountered during the last check.
    /// </summary>
    public record UpdateCheckState(
        bool UpdateAvailable,
        string? LatestVersion,
        string CurrentVersion,
        string? DownloadUrl,
        DateTime? LastCheckedUtc,
        string? ErrorMessage);

    /// <summary>
    /// Interface for the update-check service, exposing state queries and manual trigger capability.
    /// </summary>
    public interface IUpdateCheckService
    {
        /// <summary>Returns a snapshot of the current update-check state.</summary>
        UpdateCheckState GetState();

        /// <summary>Triggers an immediate update check without waiting for the periodic timer.</summary>
        Task TriggerCheckNowAsync(CancellationToken ct);
    }
}
