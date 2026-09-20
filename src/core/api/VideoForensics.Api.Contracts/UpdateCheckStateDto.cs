namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object representing the current state of update checking.
    /// Mirrors the domain UpdateCheckState record for wire transmission.
    /// </summary>
    /// <param name="UpdateAvailable">Whether a newer version is available.</param>
    /// <param name="LatestVersion">The semantic version string of the latest available release (null if no update available).</param>
    /// <param name="CurrentVersion">The semantic version string of the currently running release.</param>
    /// <param name="DownloadUrl">Direct download URL for the platform-appropriate installer asset (null if no matching asset found).</param>
    /// <param name="LastCheckedUtc">UTC timestamp of the most recent successful check attempt (null if never checked).</param>
    /// <param name="ErrorMessage">Human-readable error description from the most recent check attempt (null if no error or not yet checked).</param>
    public record UpdateCheckStateDto(
        bool UpdateAvailable,
        string? LatestVersion,
        string CurrentVersion,
        string? DownloadUrl,
        DateTime? LastCheckedUtc,
        string? ErrorMessage);
}
