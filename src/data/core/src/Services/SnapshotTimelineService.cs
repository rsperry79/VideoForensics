using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>
    /// Builds a device timeline from stored snapshot attempts. Rows are read from the database, mapped to stills (only
    /// current captures with a file on disk), and handed to the timeline builder. Each still is held for one second
    /// when it is the newest, and otherwise until the next capture or failed attempt.
    /// </summary>
    public sealed class SnapshotTimelineService : ISnapshotTimelineService
    {
        /// <summary>The timeline runs at 1 fps, so a still with no later capture is held for one frame.</summary>
        private static readonly TimeSpan LastStillHold = TimeSpan.FromSeconds(1);

        private readonly ISnapshotCaptureRepository _captures;
        private readonly ISnapshotStillMapper _stillMapper;
        private readonly ITimelineVideoBuilder _builder;
        private readonly SnapshotTimelineOptions _options;
        private readonly ILogger<SnapshotTimelineService> _logger;

        public SnapshotTimelineService(
            ISnapshotCaptureRepository captures,
            ISnapshotStillMapper stillMapper,
            ITimelineVideoBuilder builder,
            IOptions<SnapshotTimelineOptions> options,
            ILogger<SnapshotTimelineService> logger)
        {
            _captures = captures;
            _stillMapper = stillMapper;
            _builder = builder;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string?> BuildDeviceTimelineAsync(Guid deviceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, string outputPath, CancellationToken cancellationToken)
        {
            IReadOnlyList<VideoForensics.Data.Common.Entities.SnapshotCapture> rows =
                await _captures.GetAttemptsForDeviceAsync(deviceId, fromUtc, toUtc, cancellationToken);

            // Failed attempts are kept: the mapper uses them to end a still's hold, so outages show as gaps.
            List<SnapshotStillRecord> records = rows
                .Select(row => new SnapshotStillRecord(row.DeviceId, row.CapturedAtUtc, row.HttpStatus, row.MediaFilePath))
                .ToList();

            IReadOnlyList<TimelineStill> stills = _stillMapper.MapCurrentStills(
                new SnapshotStillMapRequest(records, deviceId, _options.SnapshotRoot, LastStillHold));

            // The builder rejects an empty still list, so an empty window is a normal "nothing to build" outcome.
            if (stills.Count == 0)
            {
                return null;
            }

            TimelineVideoResult result = await _builder.BuildAsync(new TimelineVideoRequest(stills, outputPath), cancellationToken);
            if (!result.Success)
            {
                _logger.LogError("Snapshot timeline build failed: {Error}", result.ErrorMessage);
                return null;
            }

            _logger.LogInformation("Snapshot timeline built from {StillCount} stills", stills.Count);
            return outputPath;
        }
    }
}
