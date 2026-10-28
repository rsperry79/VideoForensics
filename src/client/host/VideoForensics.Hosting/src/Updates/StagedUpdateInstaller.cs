using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>
    /// Update installer that stages the release zip instead of launching an installer. The staged package is applied
    /// by <see cref="StartupUpdateApplier"/> the next time the service starts, so no elevation or user action is needed.
    /// </summary>
    public sealed class StagedUpdateInstaller : IUpdateInstaller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUpdateStager _stager;
        private readonly string _updatesRoot;
        private readonly ILogger<StagedUpdateInstaller> _logger;

        /// <summary>Creates the installer.</summary>
        public StagedUpdateInstaller(IHttpClientFactory httpClientFactory, IUpdateStager stager, string updatesRoot, ILogger<StagedUpdateInstaller> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _stager = stager ?? throw new ArgumentNullException(nameof(stager));
            _updatesRoot = updatesRoot ?? throw new ArgumentNullException(nameof(updatesRoot));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Matched with EndsWith against asset names, so it is the full suffix of the update zip, not just ".zip":
        /// a release may carry other zip assets.
        /// </summary>
        public string PackageExtension => "-update-win-x64.zip";

        /// <inheritdoc />
        public async Task DownloadAndInvokeAsync(GitHubReleaseAsset asset, CancellationToken ct)
        {
            string downloads = Path.Combine(_updatesRoot, ".downloads");
            _ = Directory.CreateDirectory(downloads);
            string zipPath = Path.Combine(downloads, Path.GetFileName(asset.Name));

            try
            {
                using (HttpClient client = _httpClientFactory.CreateClient())
                {
                    await using Stream response = await client.GetStreamAsync(asset.BrowserDownloadUrl, ct);
                    await using FileStream file = File.Create(zipPath);
                    await response.CopyToAsync(file, ct);
                }

                UpdateStageResult result = await _stager.StageZipAsync(zipPath, ct);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message);
                }

                _logger.LogInformation("Downloaded and staged update '{AssetName}'; it applies on the next service start", asset.Name);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
        }
    }
}
