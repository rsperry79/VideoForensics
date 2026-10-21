using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Hosting.Contracts;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Rolls an account's snapshots up into one MP4 loop per device. The stills are the snapshot JPEGs that the scheduled
    /// pull recorded as Media rows, so the loop covers the same frames the Media list shows. Each frame is held until the
    /// next one, and the newest is held until the window end. The JPEGs are kept as evidence.
    /// </summary>
    public sealed class SnapshotRollupService : ISnapshotRollupService
    {
        /// <summary>Each camera's loop is 360 px high. Display is built later; the gap-fill builds elsewhere stay at full resolution.</summary>
        internal const int CameraLoopHeight = 360;


        /// <summary>Metadata written on each roll-up loop row. The Devices tab reads it to pick the latest loop.</summary>
        public const string SnapshotRollupMarker = "{\"type\":\"snapshot-rollup\"}";

        /// <summary>The loop's frames are each held at least this long; the builder rounds to whole seconds.</summary>
        private static readonly TimeSpan MinimumHold = TimeSpan.FromSeconds(1);

        private readonly ILocationRepository _locations;
        private readonly IDeviceRepository _devices;
        private readonly IMediaItemRepository _mediaItems;
        private readonly ITimelineVideoBuilder _builder;
        private readonly IOptions<SnapshotTimelineOptions> _options;
        private readonly ILogger<SnapshotRollupService> _logger;

        public SnapshotRollupService(
            ILocationRepository locations,
            IDeviceRepository devices,
            IMediaItemRepository mediaItems,
            ITimelineVideoBuilder builder,
            IOptions<SnapshotTimelineOptions> options,
            ILogger<SnapshotRollupService> logger)
        {
            _locations = locations;
            _devices = devices;
            _mediaItems = mediaItems;
            _builder = builder;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SnapshotRollupResult> RollUpAccountAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            var devices = new List<DeviceEntity>();
            foreach (VideoForensics.Data.Common.Entities.Location location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                devices.AddRange(await _devices.GetByLocationIdAsync(location.Id, cancellationToken));
            }

            int rolledUp = 0;
            int failed = 0;
            string? firstError = null;

            foreach (DeviceEntity device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await RollUpDeviceAsync(device, fromUtc, toUtc, cancellationToken))
                    {
                        rolledUp++;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Snapshot roll-up failed for {DeviceName}", device.Name);
                    firstError ??= $"{device.Name}: {ex.Message}";
                }
            }

            string? error = failed > 0 ? $"{failed} of {devices.Count} device(s) failed the roll-up: {firstError}" : null;
            return new SnapshotRollupResult(devices.Count, rolledUp, error);
        }

        /// <summary>Builds and registers one device's loop. Returns false when the window has no usable snapshots.</summary>
        private async Task<bool> RollUpDeviceAsync(DeviceEntity device, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyList<TimelineStill> stills = await LoadStillsAsync(device.Id, fromUtc, toUtc, cancellationToken);
            if (stills.Count == 0)
            {
                // Nothing to roll up in this window is not a failure.
                return false;
            }

            // The loop file is named by its window end, so a retried window gets a fresh path.
            string outputPath = Path.Combine(_options.Value.SnapshotRoot, "rollups", device.Id.ToString("D"), $"{toUtc:yyyyMMddHHmmss}.mp4");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            TimelineVideoResult result = await _builder.BuildAsync(new TimelineVideoRequest(stills, outputPath, MaxHeight: CameraLoopHeight), cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "the timeline build failed");
            }

            await RecordLoopAsync(device, outputPath, toUtc, cancellationToken);
            return true;
        }

        /// <summary>
        /// The window's snapshot JPEGs for one device, oldest first. Each is held until the next snapshot; the newest is
        /// held until the window end. Files that are missing from disk are skipped rather than failing the loop.
        /// </summary>
        private async Task<IReadOnlyList<TimelineStill>> LoadStillsAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            // Format, marker and time window are filtered in SQL. Only the file-on-disk check stays here: the file system is not queryable.
            IReadOnlyList<MediaItem> rows = await _mediaItems.GetSnapshotsByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, cancellationToken);
            List<MediaItem> snapshots = rows.Where(m => File.Exists(m.FilePath)).ToList();

            var stills = new List<TimelineStill>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                DateTime capturedAt = snapshots[i].RecordedAtUtc;
                DateTime holdUntil = i + 1 < snapshots.Count ? snapshots[i + 1].RecordedAtUtc : toUtc;
                TimeSpan hold = holdUntil - capturedAt;
                stills.Add(new TimelineStill(
                    snapshots[i].FilePath,
                    new DateTimeOffset(capturedAt, TimeSpan.Zero),
                    hold < MinimumHold ? MinimumHold : hold));
            }

            return stills;
        }

        /// <summary>Registers the MP4 as a Media row. RecordedAtUtc is the window end, which is unique per roll-up tick.</summary>
        private async Task RecordLoopAsync(DeviceEntity device, string mp4Path, DateTime windowEndUtc, CancellationToken cancellationToken)
        {
            byte[] hash;
            await using (FileStream stream = File.OpenRead(mp4Path))
            {
                hash = await SHA256.HashDataAsync(stream, cancellationToken);
            }

            await _mediaItems.AddAsync(new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                FileName = Path.GetFileName(mp4Path),
                FilePath = mp4Path,
                MediaFormat = "mp4",
                FileSizeBytes = new FileInfo(mp4Path).Length,
                RecordedAtUtc = windowEndUtc,
                DownloadedAtUtc = DateTime.UtcNow,
                Sha256Hash = Convert.ToHexString(hash).ToLowerInvariant(),
                IntegrityVerified = false,
                // Marks the row as a roll-up loop so the Devices tab can tell it from a Ring-downloaded MP4.
                MetadataJson = SnapshotRollupMarker,
            }, cancellationToken);
        }
    }
}
