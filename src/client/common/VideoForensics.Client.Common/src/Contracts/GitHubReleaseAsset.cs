namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// An asset (downloadable file) attached to a GitHub release.
    /// </summary>
    public record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size);
}
