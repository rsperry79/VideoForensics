using Microsoft.Extensions.Logging;

using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Ring.Services;

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

        /// <summary>Event type of the row each loop writes to the Events table. The Events tab shows it as Time Lapse.</summary>
        public const string TimelapseEventType = "timelapse";

        /// <summary>The loop's frames are each held at least this long; the builder rounds to whole seconds.</summary>
        private static readonly TimeSpan MinimumHold = TimeSpan.FromSeconds(1);

        private readonly ILocationRepository _locations;
        private readonly IDeviceWriteRepository _devices;
        private readonly IMediaItemRepository _mediaItems;
        private readonly IProviderAccountRepository _accounts;
        private readonly IStorageLocationProvider _storage;
        private readonly IEvidenceChangeNotifier _evidenceChanges;
        private readonly IEventRepository _events;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITimelineVideoBuilder _builder;
        private readonly ILogger<SnapshotRollupService> _logger;

        // Optional: when both are present, stills come from capture attempts so a failed capture ends a hold.
        private readonly ISnapshotCaptureRepository? _captures;
        private readonly ISnapshotStillMapper? _stillMapper;
        private readonly IMediaPathResolver _mediaPaths;
        private readonly IForensicsConfigurationService? _configurationService;

        public SnapshotRollupService(
            ILocationRepository locations,
            IDeviceWriteRepository devices,
            IMediaItemRepository mediaItems,
            IProviderAccountRepository accounts,
            IStorageLocationProvider storage,
            ITimelineVideoBuilder builder,
            ILogger<SnapshotRollupService> logger,
            IEventRepository events,
            IUnitOfWork unitOfWork,
            IEvidenceChangeNotifier? evidenceChanges = null,
            ISnapshotCaptureRepository? captures = null,
            ISnapshotStillMapper? stillMapper = null,
            IForensicsConfigurationService? configurationService = null,
            IMediaPathResolver? mediaPaths = null)
        {
            _mediaPaths = mediaPaths ?? PassThroughMediaPathResolver.Instance;
            _captures = captures;
            _stillMapper = stillMapper;
            _configurationService = configurationService;
            _locations = locations;
            _devices = devices;
            _mediaItems = mediaItems;
            _accounts = accounts;
            _storage = storage;
            _evidenceChanges = evidenceChanges ?? new NullEvidenceChangeNotifier();
            _events = events;
            _unitOfWork = unitOfWork;
            _builder = builder;
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
                    _logger.LogError("Snapshot roll-up failed for {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                    firstError ??= $"{device.Name}: {ex.Message}";
                }
            }

            string? error = failed > 0 ? $"{failed} of {devices.Count} device(s) failed the roll-up: {firstError}" : null;
            return new SnapshotRollupResult(devices.Count, rolledUp, error);
        }

        /// <inheritdoc />
        public async Task<SnapshotRollupResult> RollUpSegmentAsync(Guid accountId, Guid deviceId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
        {
            ProviderAccount? account = await _accounts.GetAsync(accountId, cancellationToken);
            if (account is null)
            {
                return new SnapshotRollupResult(ErrorMessage: "Account not found");
            }

            string? configuredMediaRoot = _configurationService is null
                ? null
                : (await _configurationService.LoadConfigurationAsync(string.Empty, cancellationToken)).DownloadLocation;
            string mediaRoot = _storage.GetEffectiveRoot(StorageCategory.Media, configuredMediaRoot);
            foreach (VideoForensics.Data.Common.Entities.Location location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                foreach (DeviceEntity device in await _devices.GetByLocationIdAsync(location.Id, cancellationToken))
                {
                    if (device.Id != deviceId)
                    {
                        continue;
                    }

                    string deviceFolder = MediaFolderLayout.GetDeviceFolder(mediaRoot, account.ProviderName, location.Name, device.Name);
                    try
                    {
                        bool built = await RollUpDeviceAsync(device, deviceFolder, startUtc, endUtc, cancellationToken);
                        return new SnapshotRollupResult(1, built ? 1 : 0, null);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Time-lapse segment failed for {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                        return new SnapshotRollupResult(1, 0, $"{device.Name}: {ex.Message}");
                    }
                }
            }

            return new SnapshotRollupResult(ErrorMessage: "Device not found for this account");
        }

        /// <summary>Builds and registers one device's loop. Returns false when the window has no usable snapshots.</summary>
        private async Task<bool> RollUpDeviceAsync(DeviceEntity device, string deviceFolder, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyList<TimelineStill> stills = await LoadStillsAsync(device.Id, device.Name, fromUtc, toUtc, cancellationToken);
            if (stills.Count == 0)
            {
                // Nothing to roll up in this window is not a failure.
                return false;
            }

            // The loop file is named by its window end, so a retried window gets a fresh path.
            string outputPath = Path.Combine(deviceFolder, MediaFileNamer.FormatMediaFileName(device.Name, toUtc, "snapshot-loop", "mp4"));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Build into a partial file and move it into place only when the build succeeded. A crash or failure leaves no
            // half-written MP4 under the final name, and the rerun rebuilds the same segment from the same name.
            string buildingPath = Path.Combine(Path.GetDirectoryName(outputPath)!, Path.GetFileNameWithoutExtension(outputPath) + ".building.mp4");
            TimelineVideoResult result;
            try
            {
                result = await _builder.BuildAsync(new TimelineVideoRequest(stills, buildingPath, MaxHeight: CameraLoopHeight), cancellationToken);
            }
            catch
            {
                DeletePartialQuietly(buildingPath);
                throw;
            }

            if (!result.Success)
            {
                DeletePartialQuietly(buildingPath);
                throw new InvalidOperationException(result.ErrorMessage ?? "the timeline build failed");
            }

            File.Move(buildingPath, outputPath, overwrite: true);

            await RecordLoopAsync(device, outputPath, fromUtc, toUtc, cancellationToken);
            // Success is logged so a roll-up that builds a loop leaves a trace. Only the file name is logged, not the full path.
            _logger.LogInformation("Time lapse built for {DeviceName}: {SnapshotCount} snapshots, file {FileName}", device.Name, stills.Count, Path.GetFileName(outputPath));
            return true;
        }

        /// <summary>Removes a partial MP4 left by a failed build. Missing files and I/O errors are ignored: the next run overwrites it.</summary>
        private static void DeletePartialQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best effort only. A leftover partial file is replaced by the next build of the same segment.
            }
        }

        /// <summary>
        /// The window's snapshot JPEGs for one device, oldest first. Each is held until the next snapshot; the newest is
        /// held until the window end. Files that are missing from disk are skipped rather than failing the loop.
        /// </summary>
        private async Task<IReadOnlyList<TimelineStill>> LoadStillsAsync(Guid deviceId, string deviceName, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            if (_captures is not null && _stillMapper is not null)
            {
                return await LoadStillsFromCapturesAsync(deviceId, deviceName, fromUtc, toUtc, cancellationToken);
            }

            // Format, marker and time window are filtered in SQL. Only the file-on-disk check stays here: the file system is not queryable.
            IReadOnlyList<MediaItem> rows = await _mediaItems.GetSnapshotsByDeviceAndDateRangeAsync(deviceId, fromUtc, toUtc, cancellationToken);
            var snapshots = new List<(MediaItem Row, string Path)>();
            foreach (MediaItem row in rows)
            {
                string path = await _mediaPaths.ToAbsoluteAsync(row.FilePath, cancellationToken);
                if (File.Exists(path))
                {
                    snapshots.Add((row, path));
                }
            }

            var stills = new List<TimelineStill>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                DateTime capturedAt = snapshots[i].Row.RecordedAtUtc;
                DateTime holdUntil = i + 1 < snapshots.Count ? snapshots[i + 1].Row.RecordedAtUtc : toUtc;
                TimeSpan hold = holdUntil - capturedAt;
                stills.Add(new TimelineStill(
                    snapshots[i].Path,
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
        private async Task<IReadOnlyList<TimelineStill>> LoadStillsFromCapturesAsync(Guid deviceId, string deviceName, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyList<SnapshotCapture> rows = await _captures!.GetAttemptsForDeviceAsync(deviceId, fromUtc, toUtc, cancellationToken);
            var records = new List<SnapshotStillRecord>(rows.Count);
            foreach (SnapshotCapture r in rows)
            {
                string? path = string.IsNullOrWhiteSpace(r.MediaFilePath) ? null : await _mediaPaths.ToAbsoluteAsync(r.MediaFilePath, cancellationToken);
                records.Add(new SnapshotStillRecord(r.DeviceId, r.CapturedAtUtc, r.HttpStatus, path));
            }

            // The newest 200 is the only still that can be held to the window end; the mapper cuts it short at a later failure.
            DateTimeOffset? newestStill = rows.Where(r => r.HttpStatus == 200 && !string.IsNullOrWhiteSpace(r.MediaFilePath))
                .Select(r => (DateTimeOffset?)r.CapturedAtUtc).Max();
            if (newestStill is null)
            {
                LogNoUsableStill(deviceName, rows.Count);
                return [];
            }

            TimeSpan lastHold = new DateTimeOffset(toUtc, TimeSpan.Zero) - newestStill.Value;
            IReadOnlyList<TimelineStill> stills = _stillMapper!.MapCurrentStills(new SnapshotStillMapRequest(records, deviceId, lastHold < MinimumHold ? MinimumHold : lastHold));
            if (stills.Count == 0)
            {
                LogNoUsableStill(deviceName, rows.Count);
            }

            return stills;
        }

        /// <summary>
        /// Logs why a window with capture attempts built no loop. A quiet window with no attempts is normal and not logged.
        /// Without this line a roll-up that finds no usable image records success and leaves no trace.
        /// </summary>
        private void LogNoUsableStill(string deviceName, int attemptCount)
        {
            if (attemptCount > 0)
            {
                _logger.LogWarning("Snapshot roll-up for {DeviceName} built no loop: {AttemptCount} capture attempts in the window, none with a usable image on disk", deviceName, attemptCount);
            }
        }

        /// <summary>
        /// Registers the MP4 as a Media row. The loop covers the real window [fromUtc, toUtc), so it starts at fromUtc and lasts
        /// the window's length on the timeline. The MP4's own playback length is not used: analysis keeps this value for loops.
        /// </summary>
        private async Task RecordLoopAsync(DeviceEntity device, string mp4Path, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            byte[] hash;
            await using (FileStream stream = File.OpenRead(mp4Path))
            {
                hash = await SHA256.HashDataAsync(stream, cancellationToken);
            }

            string storedPath = await _mediaPaths.ToStoredAsync(mp4Path, cancellationToken);
            long fileSizeBytes = new FileInfo(mp4Path).Length;

            // The loop row and its event row share one unit of work, so they commit together or not at all. A crash between
            // them would otherwise leave a loop the Events tab cannot show, or an event pointing at no loop.
            _ = await _unitOfWork.ExecuteAsync<int>(async ctx =>
            {
                await ctx.MediaItems.AddAsync(new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    FileName = Path.GetFileName(mp4Path),
                    FilePath = storedPath,
                    MediaFormat = "mp4",
                    MediaKind = VideoForensics.Data.Common.Entities.MediaKinds.Timelapse,
                    FileSizeBytes = fileSizeBytes,
                    RecordedAtUtc = fromUtc,
                    DurationSeconds = (toUtc - fromUtc).TotalSeconds,
                    DownloadedAtUtc = DateTime.UtcNow,
                    Sha256Hash = Convert.ToHexString(hash).ToLowerInvariant(),
                    IntegrityVerified = false,
                    // Marks the row as a roll-up loop so the Devices tab can tell it from a Ring-downloaded MP4.
                    MetadataJson = SnapshotRollupMarker,
                }, cancellationToken);

                // The Events tab reads the Events table, so the loop also gets an event row at the loop's start. It is marked
                // Downloaded so the event media puller never tries to fetch it from the provider. The window end is the key, so a
                // rerun updates the same row.
                DateTime now = DateTime.UtcNow;
                _ = await ctx.Events.UpsertAsync(new Event
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    ProviderEventId = $"{TimelapseEventType}-{toUtc:yyyyMMddHHmmss}",
                    EventType = TimelapseEventType,
                    OccurredAtUtc = fromUtc,
                    DiscoveredAtUtc = now,
                    DownloadedAtUtc = now,
                    DownloadStatus = EventDownloadStatus.Downloaded,
                }, cancellationToken);

                return 0;
            }, cancellationToken);

            await _evidenceChanges.EvidenceChangedAsync(device.Id, cancellationToken);
        }
    }
}
