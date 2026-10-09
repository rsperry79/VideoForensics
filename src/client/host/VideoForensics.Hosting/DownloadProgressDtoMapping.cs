using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>Builds the download-progress wire payload from the server's local download service.</summary>
    public static class DownloadProgressDtoMapping
    {
        /// <summary>
        /// Captures a snapshot of the service's progress state as a <see cref="DownloadProgressDto"/>.
        /// Note: drains the activity log, so each call consumes the lines it returns.
        /// </summary>
        public static DownloadProgressDto ToDownloadProgressDto(this IVideoDownloadService service)
        {
            (int index, int total, string? name) = service.GetCurrentDevice();

            return new DownloadProgressDto(
                service.GetProgress().ToDto(),
                index,
                total,
                name,
                service.DrainActivityLog(),
                service.GetPreScanCounts(),
                service.GetLastError(),
                service.GetRemainingReason());
        }
    }
}
