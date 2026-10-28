using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting.Contracts;

using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Hosting.Services
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
        private readonly IProviderAccountRepository _accounts;
        private readonly IStorageLocationProvider _storage;
        private readonly IEvidenceChangeNotifier _evidenceChanges;
        private readonly ITimelineVideoBuilder _builder;
        private readonly IOptions<SnapshotTimelineOptions> _options;
        private readonly ILogger<SnapshotRollupService> _logger;

        // Optional: when both are present, stills come from capture attempts so a failed capture ends a hold.
        private readonly ISnapshotCaptureRepository? _captures;
        private readonly ISnapshotStillMapper? _stillMapper;
        private readonly IForensicsConfigurationService? _configurationService;

        public SnapshotRollupService(
            ILocationRepository locations,
            IDeviceRepository devices,
            IMediaItemRepository mediaItems,
            IProviderAccountRepository accounts,
            IStorageLocationProvider storage,
            ITimelineVideoBuilder builder,
            IOptions<SnapshotTimelineOptions> options,
            ILogger<SnapshotRollupService> logger,
            IEvidenceChangeNotifier? evidenceChanges = null,
            ISnapshotCaptureRepository? captures = null,
            ISnapshotStillMapper? stillMapper = null,
            IForensicsConfigurationService? configurationService = null)
        {
            _captures = captures;
            _stillMapper = stillMapper;
            _configurationService = configurationService;
            _locations = locations;
            _devices = devices;
            _mediaItems = mediaItems;
            _accounts = accounts;
            _storage = storage;
            _evidenceChanges = evidenceChanges ?? new NullEvidenceChangeNotifier();
            _builder = builder;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SnapshotRollupResult> RollUpAccountAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            // Human-readable name only: raw account GUIDs must not reach the logs.
            ProviderAccount? account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return new SnapshotRollupResult(ErrorMessage: "Account not found");
            }

            // Same folder layout as the event pull and snapshot captures, so each loop sits with its device's other media.
            // Read per roll-up so the loop follows the Storage Settings Media folder, the same root the frames were written to.
            string? configuredMediaRoot = _configurationService is null
                ? null
                : (await _configurationService.LoadConfigurationAsync(string.Empty, cancellationToken)).DownloadLocation;
            string mediaRoot = _storage.GetEffectiveRoot(StorageCategory.Media, configuredMediaRoot);
            var devices = new List<(DeviceEntity Device, string DeviceFolder)>();
            foreach (VideoForensics.Data.Common.Entities.Location location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                foreach (DeviceEntity device in await _devices.GetByLocationIdAsync(location.Id, cancellationToken))
                {
                    devices.Add((device, MediaFolderLayout.GetDeviceFolder(mediaRoot, account.ProviderName, location.Name, device.Name)));
                }
            }

            int rolledUp = 0;
            int failed = 0;
            string? firstError = null;

            foreach ((DeviceEntity device, string deviceFolder) in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await RollUpDeviceAsync(device, deviceFolder, fromUtc, toUtc, cancellationToken))
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
        private async Task<bool> RollUpDeviceAsync(DeviceEntity device, string deviceFolder, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyList<TimelineStill> stills = await LoadStillsAsync(device.Id, fromUtc, toUtc, cancellationToken);
            if (stills.Count == 0)
            {
                // Nothing to roll up in this window is not a failure.
                return false;
            }

            // The loop file is named by its window end, so a retried window gets a fresh path.
            string outputPath = Path.Combine(deviceFolder, $"{toUtc:yyyyMMddHHmmss}.mp4");
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
            if (_captures is not null && _stillMapper is not null)
            {
                return await LoadStillsFromCapturesAsync(deviceId, fromUtc, toUtc, cancellationToken);
            }


            // Format, marker and time window are filtered in SQL. Only the file-on-disk check stays here: the file system is not queryable.
            IReadOnlyList<MediaItem> rows = await _mediaItems.GetSnapshotsByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, cancellationToken);
            var snapshots = rows.Where(m => File.Exists(m.FilePath)).ToList();

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

        /// <summary>
        /// Stills from the window's capture attempts, mapped the same way as the timeline. A failed attempt ends the hold of
        /// the still before it, so an outage becomes a gap that the builder fills with gray. The newest still is held to the
        /// window end unless a failure ends it first.
        /// </summary>
        private async Task<IReadOnlyList<TimelineStill>> LoadStillsFromCapturesAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyList<SnapshotCapture> rows = await _captures!.GetAttemptsForDeviceAsync(deviceId, fromUtc, toUtc, cancellationToken);
            var records = rows.Select(r => new SnapshotStillRecord(r.DeviceId, r.CapturedAtUtc, r.HttpStatus, r.MediaFilePath)).ToList();

            // The newest 200 is the only still that can be held to the window end; the mapper cuts it short at a later failure.
            DateTimeOffset? newestStill = rows.Where(r => r.HttpStatus == 200 && !string.IsNullOrWhiteSpace(r.MediaFilePath))
                .Select(r => (DateTimeOffset?)r.CapturedAtUtc).Max();
            if (newestStill is null)
            {
                return [];
            }

            TimeSpan lastHold = new DateTimeOffset(toUtc, TimeSpan.Zero) - newestStill.Value;
            return _stillMapper!.MapCurrentStills(new SnapshotStillMapRequest(records, deviceId, _options.Value.SnapshotRoot, lastHold < MinimumHold ? MinimumHold : lastHold));
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

            await _evidenceChanges.EvidenceChangedAsync(device.Id, cancellationToken);
        }
    }
}
