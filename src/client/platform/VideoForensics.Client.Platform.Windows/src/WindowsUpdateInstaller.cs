using CliWrap;

using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Windows update installer. Downloads the release .exe to the temp folder and launches it with the /passive flag
    /// for a non-interactive install. The launch is fire-and-forget so the update does not block the running app.
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

        /// <inheritdoc />
        public async Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), asset.Name);

                using (var client = new HttpClient())
                {
                    await using Stream response = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct).ConfigureAwait(true);
                    await using FileStream fileStream = File.Create(tempPath);
                    await response.CopyToAsync(fileStream, ct).ConfigureAwait(true);
                }

                // Fire-and-forget: CliWrap owns the process lifecycle, and awaiting it would block the update.
                _ = Cli.Wrap(tempPath)
                    .WithArguments("/passive")
                    .ExecuteAsync(ct);

                _logger.LogInformation("Launched installer '{AssetName}' with /passive", asset.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download/invoke installer for asset '{AssetName}'", asset.Name);
                throw;
            }
        }
    }
}
