using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Windows update installer. Currently refuses every install: the release .exe is not verified (no hash or signature).
    /// </summary>
    public class WindowsUpdateInstaller : IUpdateInstaller
    {
        private readonly ILogger<WindowsUpdateInstaller> _logger;

        public WindowsUpdateInstaller(ILogger<WindowsUpdateInstaller> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string PackageExtension => ".exe";

        /// <summary>
        /// Refuses to run an unverified installer. The release .exe has no hash or signature check, so downloading
        /// and launching it would execute unverified code with the user's rights. Throws before any network access
        /// or file write. Auto-install stays off until a verified install path exists (see the update-package signature work).
        /// </summary>
        /// <inheritdoc />
        public Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            _logger.LogWarning("Refused unverified installer '{AssetName}': no hash or signature check is available", asset.Name);
            throw new InvalidOperationException("Unverified installers are refused; auto-install is disabled until a verified install path exists.");
        }
    }
}
