namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Builds a device timeline video from stored snapshot captures. Never calls a provider.</summary>
    public interface ISnapshotTimelineService
    {
        /// <summary>
        /// Builds an MP4 of a device's stored snapshot stills in a time window, at 1 fps with each still held until the next capture.
        /// </summary>
        /// <param name="deviceId">Internal device GUID.</param>
        /// <param name="fromUtc">Window start, inclusive.</param>
        /// <param name="toUtc">Window end, inclusive.</param>
        /// <param name="outputPath">Destination MP4 path. An existing file is overwritten.</param>
        /// <param name="cancellationToken">Cancels the query and the build.</param>
        /// <returns>
        /// The output path when a video was written. Null when the window has no usable stills (nothing is built)
        /// or the build failed.
        /// </returns>
        Task<string?> BuildDeviceTimelineAsync(Guid deviceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, string outputPath, CancellationToken cancellationToken);
    }
}
