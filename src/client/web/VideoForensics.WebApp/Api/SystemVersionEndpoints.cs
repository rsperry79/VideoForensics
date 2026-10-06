using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Minimal API surface for system version information (plan §C Week 1).
    /// Exposes the current system version, build timestamp, and release channel information.
    ///
    /// These endpoints are deliberately unauthenticated (no RequireAuthorization) because
    /// clients need to check version information before pairing. Exposing version data is
    /// not a security concern - this is the same metadata users see in About dialogs and
    /// update prompts across all platforms.
    ///
    /// The version information is immutable per build, so responses are safe to cache
    /// client-side for the lifetime of the application instance.
    /// </summary>
    public static class SystemVersionEndpoints
    {
        /// <summary>
        /// Maps the system version endpoints to the web application.
        /// </summary>
        /// <param name="app">The web application to configure.</param>
        public static void MapSystemVersionEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/system");

            _ = group.MapGet("/version", GetSystemVersion)
                .WithSummary("Get system version")
                .WithDescription("Retrieves the current system version, build timestamp, and release channel. No authentication required.");

            _ = group.MapGet("/version-manifest", GetVersionManifest)
                .WithSummary("Get version manifest")
                .WithDescription("Retrieves the current system version and update availability information. No authentication required.");
        }

        /// <summary>
        /// Retrieves the current system version information.
        /// </summary>
        /// <param name="versionProvider">The system version provider service.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>200 OK with SystemVersionDto containing version, build date, and release channel.</returns>
        private static async Task<IResult> GetSystemVersion(
            ISystemVersionProvider versionProvider,
            CancellationToken ct)
        {
            string version = await versionProvider.GetCurrentVersionAsync(ct);
            DateTime? buildTimestamp = await versionProvider.GetBuildTimestampAsync(ct);
            string channel = await versionProvider.GetReleaseChannelAsync(ct);

            var dto = new SystemVersionDto(
                Version: version,
                BuildDate: buildTimestamp,
                Channel: channel);

            return Results.Ok(dto);
        }

        /// <summary>
        /// Retrieves the version manifest including current version and update availability.
        /// </summary>
        /// <param name="versionProvider">The system version provider service.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>200 OK with VersionManifestDto containing current version and update status.</returns>
        private static async Task<IResult> GetVersionManifest(
            ISystemVersionProvider versionProvider,
            CancellationToken ct)
        {
            string version = await versionProvider.GetCurrentVersionAsync(ct);
            DateTime? buildTimestamp = await versionProvider.GetBuildTimestampAsync(ct);
            string channel = await versionProvider.GetReleaseChannelAsync(ct);

            var currentVersion = new SystemVersionDto(
                Version: version,
                BuildDate: buildTimestamp,
                Channel: channel);

            // For Week 1 Days 3-4, update check integration will happen in Week 2.
            // Until then, always report no updates available.
            var manifest = new VersionManifestDto(
                CurrentVersion: currentVersion,
                LatestAvailable: null,
                DownloadUrl: null,
                ChangelogUrl: null,
                UpdateAvailable: false);

            return Results.Ok(manifest);
        }
    }
}
