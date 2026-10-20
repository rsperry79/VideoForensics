using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>Builds the download-progress wire payload from the server's local download service.</summary>
    public static class DownloadProgressDtoMapping
    {
        /// <summary>
        /// Captures a snapshot of the service's progress state as a <see cref="DownloadProgressDto"/>.
        /// Note: drains the activity log, so each call consumes the lines it returns. Only the
        /// broadcast sampler should call this; it is the single consumer whose lines fan out to all clients.
        /// </summary>
        public static DownloadProgressDto ToDownloadProgressDto(this IVideoDownloadService service, ISyncActivityLog? syncActivity = null)
        {
            (int index, int total, string? name) = service.GetCurrentDevice();

            // Sync lines follow the download lines, so one console shows both kinds in the order they were written.
            return new DownloadProgressDto(
                service.GetProgress().ToDto(),
                index,
                total,
                name,
                service.DrainActivityLog().Concat(syncActivity?.Drain() ?? []).ToArray(),
                service.GetPreScanCounts(),
                service.GetLastError(),
                service.GetRemainingReason());
        }

        /// <summary>
        /// Captures the current progress state for a single new connection without consuming anything.
        /// Activity is always empty, and <see cref="IVideoDownloadService.DrainActivityLog"/> is never
        /// called, so other clients keep their pending lines. Used for snapshot-on-connect.
        /// </summary>
        public static DownloadProgressDto ToDownloadProgressSnapshot(this IVideoDownloadService service)
        {
            (int index, int total, string? name) = service.GetCurrentDevice();

            return new DownloadProgressDto(
                service.GetProgress().ToDto(),
                index,
                total,
                name,
                Array.Empty<string>(),
                service.GetPreScanCounts(),
                service.GetLastError(),
                service.GetRemainingReason());
        }
    }
}
