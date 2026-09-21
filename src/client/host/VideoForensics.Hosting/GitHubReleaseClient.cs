using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Information about a GitHub release fetched from the GitHub Releases REST API.
    /// </summary>
    public record GitHubReleaseInfo(string TagName, string HtmlUrl, bool Draft, bool Prerelease, IReadOnlyList<GitHubReleaseAsset> Assets);

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
            return await FetchReleaseAsync("repos/rsperry79/VideoForensics/releases/latest", ct);
        }

        public async Task<GitHubReleaseInfo?> GetLatestTestingReleaseAsync(CancellationToken ct)
        {
            return await FetchReleaseAsync("repos/rsperry79/VideoForensics/releases/tags/testing", ct);
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

                return new GitHubReleaseInfo(
                    releaseDto.TagName,
                    releaseDto.HtmlUrl,
                    releaseDto.Draft,
                    releaseDto.Prerelease,
                    assets
                );
            }
            catch
            {
                // Catch any exception (JSON parse errors, network errors, etc.) and return null rather than propagating.
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
