namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Provides access to system version information including semantic version, build timestamp, and release channel.
    /// </summary>
    public interface ISystemVersionProvider
    {
        /// <summary>
        /// Gets the current semantic version of the system.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the semantic version string (e.g., "1.0.5-alpha.1+build.23").</returns>
        Task<string> GetCurrentVersionAsync(CancellationToken ct);

        /// <summary>
        /// Gets the build timestamp indicating when this version was compiled.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the build timestamp in UTC, or null if unavailable.</returns>
        Task<DateTime?> GetBuildTimestampAsync(CancellationToken ct);

        /// <summary>
        /// Gets the release channel of the current version.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the release channel ("dev", "testing", or "stable").</returns>
        Task<string> GetReleaseChannelAsync(CancellationToken ct);
    }
}
