namespace VideoForensics.Api.Contracts;

/// <summary>
/// Represents version information for the system, including version, build date, and release channel.
/// </summary>
/// <param name="Version">The semantic version string (e.g., "1.0.5-alpha.1+build.23").</param>
/// <param name="BuildDate">The date and time when this build was created, in UTC. Null if build date is unavailable.</param>
/// <param name="Channel">The release channel ("dev", "testing", or "stable").</param>
public record SystemVersionDto(
    string Version,
    DateTime? BuildDate = null,
    string Channel = "stable"
);

/// <summary>
/// Manifest of version information including the current version and available update details.
/// </summary>
/// <param name="CurrentVersion">The currently running system version.</param>
/// <param name="LatestAvailable">The latest available version, if an update is available. Null if the system is already at the latest version or update information is unavailable.</param>
/// <param name="DownloadUrl">URL where the latest update can be downloaded. Null if no update is available or download URL is not applicable.</param>
/// <param name="ChangelogUrl">URL to the changelog for the latest version. Null if changelog is not available.</param>
/// <param name="UpdateAvailable">Indicates whether an update to a newer version is available.</param>
public record VersionManifestDto(
    SystemVersionDto CurrentVersion,
    SystemVersionDto? LatestAvailable = null,
    string? DownloadUrl = null,
    string? ChangelogUrl = null,
    bool UpdateAvailable = false
);
