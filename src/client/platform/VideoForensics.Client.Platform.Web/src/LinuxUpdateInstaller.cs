using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Web
{
    /// <summary>
    /// Debian/Linux update installer for the server host. Downloads the release .deb to a pending-update location;
    /// the admin must complete the dpkg install manually since it requires root privileges.
    /// </summary>
    public sealed class LinuxUpdateInstaller : IUpdateInstaller
    {
        private readonly ILogger<LinuxUpdateInstaller> _logger;

        public LinuxUpdateInstaller(ILogger<LinuxUpdateInstaller> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string PackageExtension => ".deb";

        public async Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            try
            {
                await DownloadForLinuxAsync(asset, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download/invoke installer for asset '{AssetName}'", asset.Name);
                throw;
            }
        }

        private static async Task DownloadForLinuxAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            // On Linux, download to a well-known pending-update location (e.g., /var/lib/videoforensics/pending-update/).
            // The admin will need to manually complete the dpkg install.
            string pendingDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VideoForensics",
                "pending-update");

            _ = System.IO.Directory.CreateDirectory(pendingDir);

            string pendingPath = System.IO.Path.Combine(pendingDir, asset.Name);

            using var client = new HttpClient();
            await using Stream response = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct);
            await using FileStream fileStream = System.IO.File.Create(pendingPath);
            await response.CopyToAsync(fileStream, ct);

            // On Linux, the admin must manually run: sudo dpkg -i /path/to/the.deb
            // Log this as info so the system log shows where to find the file.
        }
    }
}
