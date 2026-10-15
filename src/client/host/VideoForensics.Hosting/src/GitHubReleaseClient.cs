using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Information about a GitHub release fetched from the GitHub Releases REST API.
    /// <paramref name="Version"/> is read from the release's <c>version.json</c> asset when present; it is null otherwise.
    /// Moving tags such as "Release" and "Testing" are not version numbers, so callers should prefer Version over TagName.
    /// </summary>
    public record GitHubReleaseInfo(string TagName, string HtmlUrl, bool Draft, bool Prerelease, IReadOnlyList<GitHubReleaseAsset> Assets, string? Version = null);

    /// <summary>
    /// An asset (downloadable file) attached to a GitHub release.
    /// </summary>
    public record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size);

    /// <summary>
    /// Fetches release information from the GitHub Releases REST API.
    /// </summary>
    public interface IGitHubReleaseClient
    {
        /// <summary>
        /// Fetches the latest stable (non-draft, non-prerelease) release.
        /// Returns null if the API call fails, the response cannot be parsed, or the release is not found.
        /// </summary>
        Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct);

        /// <summary>
        /// Fetches the testing channel prerelease (the rolling "testing" release on the rolling "testing" tag).
        /// Returns null if the API call fails, the response cannot be parsed, or the release is not found.
        /// </summary>
        Task<GitHubReleaseInfo?> GetLatestTestingReleaseAsync(CancellationToken ct);
    }

    /// <summary>
    /// Implementation of IGitHubReleaseClient that calls the GitHub Releases REST API.
    /// Handles errors gracefully, returning null on any failure (HTTP error, malformed JSON) rather than throwing.
    /// </summary>
    public class GitHubReleaseClient : IGitHubReleaseClient
    {
        /// <summary>Name of the release asset CI publishes to carry the authoritative version string.</summary>
        private const string VersionAssetName = "version.json";

        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of GitHubReleaseClient.
        /// The provided HttpClient should have its BaseAddress set to https://api.github.com/ by the caller (typically via IHttpClientFactory in DI setup).
        /// </summary>
        public GitHubReleaseClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct)
        {
            // Fetch the release tagged with "Release" (main branch latest)
            return await FetchReleaseAsync("repos/rsperry79/VideoForensics/releases/tags/Release", ct);
        }

        public async Task<GitHubReleaseInfo?> GetLatestTestingReleaseAsync(CancellationToken ct)
        {
            // Fetch the release tagged with "Testing" (dev branch latest)
            return await FetchReleaseAsync("repos/rsperry79/VideoForensics/releases/tags/Testing", ct);
        }

        private async Task<GitHubReleaseInfo?> FetchReleaseAsync(string endpoint, CancellationToken ct)
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(endpoint, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string content = await response.Content.ReadAsStringAsync(ct);
                var releaseDto = JsonSerializer.Deserialize<GitHubReleaseDto>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                if (releaseDto == null)
                {
                    return null;
                }

                var assets = releaseDto.Assets?.Select(a => new GitHubReleaseAsset(a.Name, a.BrowserDownloadUrl, a.Size)).ToList()
                    ?? new List<GitHubReleaseAsset>();

                // Version metadata is best-effort: a missing or broken version.json must not hide the release itself.
                string? version = await TryFetchVersionAsync(assets, ct);

                return new GitHubReleaseInfo(
                    releaseDto.TagName,
                    releaseDto.HtmlUrl,
                    releaseDto.Draft,
                    releaseDto.Prerelease,
                    assets,
                    version
                );
            }
            catch
            {
                // Catch any exception (JSON parse errors, network errors, etc.) and return null rather than propagating.
                return null;
            }
        }

        /// <summary>
        /// Reads the "version" property from the release's version.json asset.
        /// Returns null when the asset is absent, the download fails, or the JSON is malformed; never throws.
        /// </summary>
        /// <remarks>
        /// The CI-published version.json exists because the "Release" and "Testing" tags are moving labels,
        /// not version numbers, so the tag cannot carry the version.
        /// </remarks>
        private async Task<string?> TryFetchVersionAsync(IReadOnlyList<GitHubReleaseAsset> assets, CancellationToken ct)
        {
            GitHubReleaseAsset? versionAsset = assets.FirstOrDefault(a => a.Name == VersionAssetName);
            if (versionAsset == null)
            {
                return null;
            }

            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(versionAsset.BrowserDownloadUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(ct);
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("version", out JsonElement versionElement)
                    && versionElement.ValueKind == JsonValueKind.String)
                {
                    string? version = versionElement.GetString();
                    return string.IsNullOrWhiteSpace(version) ? null : version;
                }

                return null;
            }
            catch
            {
                // Malformed JSON, network failure, etc. -- the caller falls back to the tag.
                return null;
            }
        }

        /// <summary>
        /// Internal DTO class for deserializing GitHub release JSON response.
        /// Uses snake_case naming policy to match GitHub API field names.
        /// </summary>
        private class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubReleaseAssetDto>? Assets { get; set; }
        }

        /// <summary>
        /// Internal DTO class for deserializing GitHub release asset JSON response.
        /// </summary>
        private class GitHubReleaseAssetDto
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }
    }
}
