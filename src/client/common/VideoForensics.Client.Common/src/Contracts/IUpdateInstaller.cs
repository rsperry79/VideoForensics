namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Platform-specific update installer.
    /// Windows (<c>VideoForensics.Client.Platform.Windows</c>): downloads to temp and launches the installer with the /passive flag.
    /// Debian/Linux (Hosting default): downloads to a pending-update location; does not auto-invoke dpkg (requires root + manual admin action).
    /// </summary>
    public interface IUpdateInstaller
    {
        /// <summary>
        /// File extension of the release asset this platform installs (for example ".exe" or ".deb").
        /// The update check picks the matching asset from each release.
        /// </summary>
        string PackageExtension { get; }

        /// <summary>Downloads the release asset and invokes the installer on the current platform.</summary>
        Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct);
    }
}
