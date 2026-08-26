using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Ring.Services
{
    public class RingMediaDownloadService : IMediaDownloadService
    {
        private readonly ILogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly IVideoForensicsDataClient _dataClient;
        private DownloadStatus _currentStatus = new(IsDownloading: false, FilesCompleted: 0, FilesTotal: 0, BytesDownloaded: 0);

        // Per-item outcomes (one line per file, success or failure) queued as they happen so a
        // polling UI can show a live feed alongside the aggregate GetStatus() counters.
        private readonly ConcurrentQueue<string> _activityLog = new();

        private const int MaxRetries = 3;
        private const int InitialDelayMs = 1000;
        private const int MaxDelayMs = 30000;

        // Files within a single device download concurrently (devices themselves stay sequential —
        // see VideoDownloadServiceAdapter — to keep the deliberate inter-device rate-limit backoff).
        // Kept modest since Ring's API throttles per-account, not just per-connection.
        private const int MaxConcurrentFileDownloads = 3;

        // Guards _currentStatus and the per-device counters below while multiple file downloads for
        // the same device update them concurrently.
        private readonly object _statusLock = new();

        // How many files are actively being fetched from Ring right now (as opposed to skipped
        // because they already exist on disk) — surfaced via DownloadStatus.ActiveConnections.
        private int _activeDownloads;

        // GetDoorbotsHistory returns the FULL account history (all devices), not just one device's.
        // Cache it so a sequential per-device download loop doesn't re-fetch the same data N times.
        private readonly SemaphoreSlim _historyCacheLock = new(1, 1);
        private DateTime? _cachedHistoryStart;
        private DateTime? _cachedHistoryEnd;
        private List<Entities.DoorbotHistoryEvent>? _cachedHistoryEvents;

        // Cache the User/Account/Location identity resolution so we only do it once per process
        private readonly SemaphoreSlim _identityResolutionLock = new(1, 1);
        private Guid? _cachedProviderAccountId;
        private Guid? _cachedLocationId;

        // Per-device Guid cache to avoid redundant EnsureDeviceAsync calls for the same providerDeviceId
        private readonly ConcurrentDictionary<string, Guid> _deviceIdCache = new();

        public RingMediaDownloadService(ILogger logger, ISessionProvider sessionProvider, IVideoForensicsDataClient dataClient)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _dataClient = dataClient ?? throw new ArgumentNullException(nameof(dataClient));
        }

        public async Task<DownloadResult> DownloadVideosAsync(string deviceId, string outputPath, DateTime startDate,
            DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Downloading videos for device {DeviceId} from {StartDate} to {EndDate}",
                    deviceId, startDate, endDate);

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Download failed: not authenticated"
                    );
                }

                Directory.CreateDirectory(outputPath);

                var events = await GetHistoryEventsAsync(session, startDate, endDate);
                var relevantEvents = events.Where(e => e.Doorbot?.Id.ToString() == deviceId).ToList();

                // Track the latest successfully downloaded event's timestamp for watermark advancement
                DateTime latestSuccessfulTime = startDate;

                _logger.LogInformation(
                    "History returned {TotalEvents} total event(s) for the account; {MatchCount} matched deviceId {DeviceId}. Sample doorbot ids in history: {Sample}",
                    events.Count, relevantEvents.Count, deviceId,
                    string.Join(", ", events.Select(e => e.Doorbot?.Id).Distinct().Take(5)));

                var downloadedFiles = 0;
                var downloadedBytes = 0L;
                var metadataFilesWritten = 0;
                var mediaFilesValidated = 0;
                var validatedFiles = new List<string>();

                _activeDownloads = 0;
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = relevantEvents.Count, FilesCompleted = 0, BytesDownloaded = 0, ActiveConnections = 0 };

                // Resolve and cache the device once at batch start to avoid per-item lookups.
                // This ensures all watermark updates within the batch operate on consistent device state.
                var deviceGuid = await EnsureDeviceIdentityAsync(deviceId, relevantEvents.FirstOrDefault()?.Doorbot?.Description ?? deviceId, cancellationToken);

                // Cancels the in-flight batch either on real caller cancellation or the moment any
                // one file hits a rate limit — matching the previous sequential loop's "stop on
                // first rate limit" behavior, but without blocking files already in flight.
                using var rateLimitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                try
                {
                    await Parallel.ForEachAsync(
                        relevantEvents,
                        new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentFileDownloads, CancellationToken = rateLimitCts.Token },
                        async (@event, itemToken) =>
                        {
                            var fileName = Path.Combine(outputPath,
                                $"{deviceId}_{@event.CreatedAtDateTime:yyyyMMdd_HHmmss}.mp4");

                            // Use the device GUID resolved at batch start; all events are for this same device
                            // (filtered by deviceId above). Avoids per-item redundant lookups.

                            // Check both filesystem and DB for existing download
                            var eventIdStr = @event.Id?.ToString() ?? "unknown";
                            var alreadyDownloadedInDb = await _dataClient.IsMediaAlreadyDownloadedAsync(deviceGuid, eventIdStr, itemToken);

                            if ((File.Exists(fileName) && new FileInfo(fileName).Length > 0) || alreadyDownloadedInDb)
                            {
                                var existingSize = new FileInfo(fileName).Length > 0 ? new FileInfo(fileName).Length : 0;

                                // Compute hash for existing file if it exists on disk
                                string? sha256Hash = null;
                                if (File.Exists(fileName) && existingSize > 0)
                                {
                                    try
                                    {
                                        var hashBytes = await SHA256.HashDataAsync(File.OpenRead(fileName), itemToken);
                                        sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to compute hash for existing file {FileName}", fileName);
                                    }
                                }

                                var wroteMetadata = !File.Exists(Path.ChangeExtension(fileName, ".json")) &&
                                    WriteMetadataFile(fileName, deviceId, @event, existingSize, "mp4");

                                lock (_statusLock)
                                {
                                    downloadedFiles++;
                                    downloadedBytes += existingSize;
                                    mediaFilesValidated++;
                                    validatedFiles.Add(fileName);
                                    if (wroteMetadata)
                                        metadataFilesWritten++;

                                    _currentStatus = _currentStatus with
                                    {
                                        FilesCompleted = downloadedFiles,
                                        BytesDownloaded = downloadedBytes,
                                        CurrentFile = fileName,
                                        ActiveConnections = Volatile.Read(ref _activeDownloads)
                                    };

                                    // Update watermark for existing files too, so if batch fails later,
                                    // we don't re-attempt files we've already validated
                                    var eventTime = (@event.CreatedAtDateTime ?? DateTime.UtcNow).ToUniversalTime();
                                    if (eventTime > latestSuccessfulTime)
                                        latestSuccessfulTime = eventTime;
                                }

                                _activityLog.Enqueue($"[dim]○[/] {Path.GetFileName(fileName)} already exists, skipped");
                                return;
                            }

                            try
                            {
                                _activityLog.Enqueue($"[blue]▸[/] Downloading {Path.GetFileName(fileName)}...");

                                Interlocked.Increment(ref _activeDownloads);
                                try
                                {
                                    await RetryWithBackoffAsync(async () =>
                                    {
                                        await session.GetDoorbotHistoryRecording(@event, fileName);
                                    }, $"video for event {@event.Id}", itemToken);
                                }
                                finally
                                {
                                    Interlocked.Decrement(ref _activeDownloads);
                                }

                                if (File.Exists(fileName))
                                {
                                    var downloadedSize = new FileInfo(fileName).Length;
                                    var validated = downloadedSize > 0;
                                    var wroteMetadata = WriteMetadataFile(fileName, deviceId, @event, downloadedSize, "mp4");

                                    // Compute SHA-256 hash for the downloaded file
                                    string? sha256Hash = null;
                                    if (validated)
                                    {
                                        try
                                        {
                                            var hashBytes = await SHA256.HashDataAsync(File.OpenRead(fileName), itemToken);
                                            sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to compute SHA-256 hash for {FileName}", fileName);
                                        }
                                    }

                                    // Record download event in database
                                    if (sha256Hash != null)
                                    {
                                        try
                                        {
                                            var downloadEvent = new DownloadEvent
                                            {
                                                Id = Guid.NewGuid(),
                                                DeviceId = deviceGuid,
                                                ProviderEventId = eventIdStr,
                                                EventType = @event.Kind,
                                                Answered = @event.Answered,
                                                Favorite = @event.Favorite,
                                                EventOccurredAtUtc = (@event.CreatedAtDateTime ?? DateTime.UtcNow).ToUniversalTime(),
                                                RecordingStatus = @event.Recording?.Status,
                                                DownloadStartedUtc = DateTime.UtcNow,
                                                DownloadCompletedUtc = DateTime.UtcNow,
                                                Success = true,
                                                AttemptCount = 1,
                                                AppVersion = typeof(RingMediaDownloadService).Assembly.GetName().Version?.ToString() ?? "unknown"
                                            };

                                            var mediaItem = new MediaItem
                                            {
                                                Id = Guid.NewGuid(),
                                                DeviceId = deviceGuid,
                                                DownloadEventId = downloadEvent.Id,
                                                FileName = Path.GetFileName(fileName),
                                                FilePath = fileName,
                                                MediaFormat = "mp4",
                                                FileSizeBytes = downloadedSize,
                                                RecordedAtUtc = (@event.CreatedAtDateTime ?? DateTime.UtcNow).ToUniversalTime(),
                                                DownloadedAtUtc = DateTime.UtcNow,
                                                Sha256Hash = sha256Hash,
                                                IntegrityVerified = false
                                            };

                                            await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, itemToken);

                                            // Update watermark after successful DownloadEvent recording using the authoritative EventOccurredAtUtc
                                            try
                                            {
                                                await _dataClient.UpdateDeviceWatermarkAsync(deviceGuid, downloadEvent.EventOccurredAtUtc, itemToken);
                                                _logger.LogInformation("Watermark advanced to {Timestamp} after recording {EventId}",
                                                    downloadEvent.EventOccurredAtUtc, downloadEvent.ProviderEventId);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogWarning(ex, "Failed to update watermark after recording event {EventId}", downloadEvent.ProviderEventId);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to record download event in database for {FileName}. Download succeeded but database record was not created.", fileName);
                                        }
                                    }

                                    lock (_statusLock)
                                    {
                                        downloadedFiles++;
                                        downloadedBytes += downloadedSize;
                                        if (validated)
                                        {
                                            mediaFilesValidated++;
                                            validatedFiles.Add(fileName);
                                        }
                                        if (wroteMetadata)
                                            metadataFilesWritten++;

                                        _currentStatus = _currentStatus with
                                        {
                                            FilesCompleted = downloadedFiles,
                                            BytesDownloaded = downloadedBytes,
                                            CurrentFile = fileName,
                                            ActiveConnections = Volatile.Read(ref _activeDownloads)
                                        };
                                    }

                                    _activityLog.Enqueue($"[green]✓[/] {Path.GetFileName(fileName)} ({FormatBytes(downloadedSize)}) complete");
                                }
                            }
                            catch (OperationCanceledException) when (itemToken.IsCancellationRequested)
                            {
                                // Cooperative stop — either caller cancellation or a sibling file's
                                // rate limit triggering rateLimitCts. Not a per-file failure.
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download video for event {EventId}", @event.Id);
                                _activityLog.Enqueue($"[red]✗[/] event {@event.Id}: {EscapeMarkup(ex.Message)}");
                                if (IsRateLimitError(ex))
                                {
                                    _logger.LogInformation("Rate limit detected. Stopping remaining downloads for this device.");
                                    rateLimitCts.Cancel();
                                }
                            }
                        });
                }
                catch (OperationCanceledException)
                {
                    // Either caller cancellation or an internal rate-limit-triggered stop — both
                    // mean "stop early", matching the previous sequential loop's plain `break`.
                    // Counts accumulated above still reflect whatever completed before the stop.
                }

                _currentStatus = _currentStatus with { IsDownloading = false };

                _logger.LogInformation("Downloaded {FileCount} videos ({Bytes} bytes) for device {DeviceId}",
                    downloadedFiles, downloadedBytes, deviceId);

                return new DownloadResult(
                    Success: true,
                    FilesDownloaded: downloadedFiles,
                    BytesDownloaded: downloadedBytes,
                    MetadataFilesWritten: metadataFilesWritten,
                    MediaFilesValidated: mediaFilesValidated,
                    ValidatedFiles: validatedFiles,
                    FilesMatched: relevantEvents.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading videos for device {DeviceId}", deviceId);
                _currentStatus = _currentStatus with { IsDownloading = false };
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Download failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Downloads the single latest available snapshot for a device. Ring does not expose historical,
        /// per-event snapshots (DoorbotHistoryEvent.SnapshotUrl is always empty in practice) — the only
        /// snapshot API available is "the device's current/most-recent snapshot," optionally refreshed
        /// on demand. startDate/endDate are accepted for interface compatibility but have no effect here.
        /// </summary>
        public async Task<DownloadResult> DownloadSnapshotsAsync(string deviceId, string outputPath, DateTime startDate,
            DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Downloading latest snapshot for device {DeviceId}", deviceId);

                var session = _sessionProvider.GetSession();
                if (session == null)
                {
                    _logger.LogError("Not authenticated: Session is null");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Download failed: not authenticated"
                    );
                }

                if (!int.TryParse(deviceId, out var doorbotId))
                {
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: $"Invalid device id: {deviceId}"
                    );
                }

                Directory.CreateDirectory(outputPath);
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = 1, FilesCompleted = 0 };

                // Best-effort: ask Ring to capture a fresh snapshot before fetching it. Not every
                // device/plan supports on-demand refresh, so failures here are non-fatal — fall back
                // to whatever snapshot Ring already has on hand.
                try
                {
                    await session.UpdateSnapshot(doorbotId);
                    await Task.Delay(2000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Could not trigger a fresh snapshot for device {DeviceId}; using latest available instead", deviceId);
                }

                var fileName = Path.Combine(outputPath, $"{deviceId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");

                _activityLog.Enqueue($"[blue]▸[/] Downloading {Path.GetFileName(fileName)}...");

                await RetryWithBackoffAsync(async () =>
                {
                    await session.GetLatestSnapshot(doorbotId, fileName);
                }, $"latest snapshot for device {deviceId}", cancellationToken);

                if (!File.Exists(fileName))
                {
                    _currentStatus = _currentStatus with { IsDownloading = false };
                    _activityLog.Enqueue($"[red]✗[/] device {deviceId}: no snapshot available");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "No snapshot available for this device"
                    );
                }

                // Ring returns a tiny non-image error/placeholder body (observed: a fixed 10 bytes)
                // instead of an HTTP error when a device has no snapshot to give — e.g. an offline
                // stickup cam. Validate the JPEG magic number so we don't report a corrupt file as a
                // successful download.
                if (!IsValidJpeg(fileName))
                {
                    _logger.LogWarning("Device {DeviceId} returned a non-image response instead of a snapshot (device likely offline)", deviceId);
                    File.Delete(fileName);
                    _currentStatus = _currentStatus with { IsDownloading = false };
                    _activityLog.Enqueue($"[red]✗[/] device {deviceId}: offline, no snapshot available");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "No snapshot available for this device (device may be offline)"
                    );
                }

                var fileSize = new FileInfo(fileName).Length;
                var metadataWritten = WriteSnapshotMetadataFile(fileName, deviceId, fileSize);

                // Resolve device identity and record snapshot download
                var deviceGuid = await EnsureDeviceIdentityAsync(deviceId, deviceId, cancellationToken);

                // Compute SHA-256 hash for snapshot
                string? sha256Hash = null;
                if (fileSize > 0)
                {
                    try
                    {
                        var hashBytes = await SHA256.HashDataAsync(File.OpenRead(fileName), cancellationToken);
                        sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compute SHA-256 hash for snapshot {FileName}", fileName);
                    }
                }

                // Record snapshot download in database
                if (sha256Hash != null)
                {
                    try
                    {
                        var snapshotEventId = $"snapshot-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        var downloadEvent = new DownloadEvent
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = deviceGuid,
                            ProviderEventId = snapshotEventId,
                            EventType = "snapshot",
                            Answered = false,
                            Favorite = false,
                            EventOccurredAtUtc = DateTime.UtcNow,
                            RecordingStatus = null,
                            DownloadStartedUtc = DateTime.UtcNow,
                            DownloadCompletedUtc = DateTime.UtcNow,
                            Success = true,
                            AttemptCount = 1,
                            AppVersion = typeof(RingMediaDownloadService).Assembly.GetName().Version?.ToString() ?? "unknown"
                        };

                        var mediaItem = new MediaItem
                        {
                            Id = Guid.NewGuid(),
                            DeviceId = deviceGuid,
                            DownloadEventId = downloadEvent.Id,
                            FileName = Path.GetFileName(fileName),
                            FilePath = fileName,
                            MediaFormat = "jpg",
                            FileSizeBytes = fileSize,
                            RecordedAtUtc = DateTime.UtcNow,
                            DownloadedAtUtc = DateTime.UtcNow,
                            Sha256Hash = sha256Hash,
                            IntegrityVerified = false
                        };

                        await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record snapshot download event in database for {FileName}. Download succeeded but database record was not created.", fileName);
                    }
                }

                _currentStatus = _currentStatus with
                {
                    IsDownloading = false,
                    FilesCompleted = 1,
                    BytesDownloaded = fileSize,
                    CurrentFile = fileName
                };

                _activityLog.Enqueue($"[green]✓[/] {Path.GetFileName(fileName)} ({FormatBytes(fileSize)}) complete");

                _logger.LogInformation("Downloaded latest snapshot ({Bytes} bytes) for device {DeviceId}", fileSize, deviceId);

                return new DownloadResult(
                    Success: true,
                    FilesDownloaded: 1,
                    BytesDownloaded: fileSize,
                    MetadataFilesWritten: metadataWritten ? 1 : 0,
                    MediaFilesValidated: fileSize > 0 ? 1 : 0,
                    ValidatedFiles: fileSize > 0 ? new List<string> { fileName } : new List<string>(),
                    FilesMatched: 1
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading snapshot for device {DeviceId}", deviceId);
                _currentStatus = _currentStatus with { IsDownloading = false };
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Download failed: {ex.Message}"
                );
            }
        }

        public DownloadStatus GetStatus() => _currentStatus;

        public IReadOnlyList<string> DrainActivityLog()
        {
            var items = new List<string>();
            while (_activityLog.TryDequeue(out var item))
            {
                items.Add(item);
            }
            return items;
        }

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024;
            const double mb = kb * 1024;
            if (bytes >= mb)
                return $"{bytes / mb:F1} MB";
            if (bytes >= kb)
                return $"{bytes / kb:F1} KB";
            return $"{bytes} bytes";
        }

        private static string EscapeMarkup(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("[", "[[").Replace("]", "]]");
        }

        private static string BuildNamespacedOutputPath(string outputPath, string providerName, string accountDisplayName)
        {
            return Path.Combine(outputPath, providerName, accountDisplayName);
        }

        private bool WriteMetadataFile(string mediaFilePath, string deviceId, Entities.DoorbotHistoryEvent @event, long fileSizeBytes, string mediaFormat)
        {
            try
            {
                var cv = @event.CvProperties;
                var metadata = new RingEventMetadata(
                    FileName: Path.GetFileName(mediaFilePath),
                    DeviceId: deviceId,
                    DeviceName: @event.Doorbot?.Description ?? deviceId,
                    EventId: @event.Id?.ToString() ?? "unknown",
                    EventType: @event.Kind,
                    Answered: @event.Answered,
                    Favorite: @event.Favorite,
                    RecordedAt: @event.CreatedAtDateTime ?? DateTime.MinValue,
                    FileSizeBytes: fileSizeBytes,
                    MediaFormat: mediaFormat,
                    RecordingStatus: @event.Recording?.Status,
                    SnapshotUrl: @event.SnapshotUrl,
                    ComputerVision: cv == null ? null : new RingCvMetadata(
                        PersonDetected: cv.PersonDetected,
                        StreamBroken: cv.StreamBroken,
                        DetectionType: cv.DetectionType,
                        Detections: cv.DetectionTypes?
                            .Select(d => new RingCvDetection(d.DetectionType, d.VerifiedTimestamps))
                            .ToList(),
                        FullDescription: cv.FullDescription,
                        ShortDescription: cv.ShortDescription,
                        Similarity: cv.Similarity,
                        Anomaly: cv.Anomaly,
                        Tags: cv.Tags,
                        SecurityAlerts: cv.SecurityAlerts == null ? null :
                            new RingSecurityAlerts(cv.SecurityAlerts.Severity, cv.SecurityAlerts.Alerts),
                        Profiles: cv.Profiles?
                            .Select(p => new RingCvProfile(p.Id, p.Name, p.Confidence))
                            .ToList(),
                        Zones: cv.DetectionDetails?.Zones?
                            .Select(z => new RingCvZone(z.Id, z.Name, z.Confidence))
                            .ToList(),
                        DetectionConfidence: cv.DetectionDetails?.Confidence,
                        ModelVersion: cv.DetectionDetails?.ModelVersion
                    )
                );

                var metadataPath = Path.ChangeExtension(mediaFilePath, ".json");
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
                File.WriteAllText(metadataPath, json);

                if (!ValidateJsonSidecar(metadataPath, "event metadata"))
                {
                    _logger.LogWarning("Validation failed for metadata sidecar {MetadataPath}", metadataPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write metadata file for {MediaFilePath}", mediaFilePath);
                return false;
            }
        }

        private record RingEventMetadata(
            string FileName,
            string DeviceId,
            string DeviceName,
            string EventId,
            string? EventType,
            bool Answered,
            bool Favorite,
            DateTime RecordedAt,
            long FileSizeBytes,
            string MediaFormat,
            string? RecordingStatus,
            string? SnapshotUrl,
            RingCvMetadata? ComputerVision
        );

        /// <summary>Ring AI computer-vision analysis for an event, when available (not all events are CV-evaluated).</summary>
        private record RingCvMetadata(
            bool? PersonDetected,
            bool? StreamBroken,
            string? DetectionType,
            List<RingCvDetection>? Detections,
            string? FullDescription,
            string? ShortDescription,
            double? Similarity,
            double? Anomaly,
            List<string>? Tags,
            RingSecurityAlerts? SecurityAlerts,
            List<RingCvProfile>? Profiles,
            List<RingCvZone>? Zones,
            double? DetectionConfidence,
            string? ModelVersion
        );

        private record RingCvDetection(string? Type, List<long>? VerifiedTimestampsEpochMs);
        private record RingSecurityAlerts(string? Severity, List<string>? Alerts);
        private record RingCvProfile(string? Id, string? Name, double? Confidence);
        private record RingCvZone(string? Id, string? Name, double? Confidence);

        private async Task<Guid> EnsureDeviceIdentityAsync(string providerDeviceId, string deviceName, CancellationToken ct)
        {
            // Check per-device cache first
            if (_deviceIdCache.TryGetValue(providerDeviceId, out var cachedDeviceId))
            {
                return cachedDeviceId;
            }

            // Resolve User/Account/Location once and cache them
            await _identityResolutionLock.WaitAsync(ct);
            try
            {
                if (!_cachedProviderAccountId.HasValue)
                {
                    var (user, account) = await _dataClient.EnsureUserAndAccountAsync(
                        "Ring",
                        "default",
                        "default",
                        null,
                        ct);
                    _cachedProviderAccountId = account.Id;
                }

                if (!_cachedLocationId.HasValue)
                {
                    var location = await _dataClient.EnsureLocationAsync(
                        _cachedProviderAccountId.Value,
                        "default",
                        "default",
                        null,
                        ct);
                    _cachedLocationId = location.Id;
                }
            }
            finally
            {
                _identityResolutionLock.Release();
            }

            // Now resolve the device
            var device = await _dataClient.EnsureDeviceAsync(
                _cachedLocationId.Value,
                providerDeviceId,
                deviceName,
                "camera",
                true,
                ct);

            // Cache in the per-device dictionary
            _deviceIdCache.TryAdd(providerDeviceId, device.Id);
            return device.Id;
        }

        private async Task<List<Entities.DoorbotHistoryEvent>> GetHistoryEventsAsync(Session session, DateTime startDate, DateTime endDate)
        {
            await _historyCacheLock.WaitAsync();
            try
            {
                if (_cachedHistoryEvents != null && _cachedHistoryStart == startDate && _cachedHistoryEnd == endDate)
                {
                    _logger.LogInformation("Reusing cached doorbot history for {StartDate} to {EndDate} ({Count} events)",
                        startDate, endDate, _cachedHistoryEvents.Count);
                    return _cachedHistoryEvents;
                }

                _logger.LogInformation("Fetching doorbot history for {StartDate} to {EndDate}", startDate, endDate);
                var events = await session.GetDoorbotsHistory(startDate, endDate);
                _cachedHistoryEvents = events ?? new List<Entities.DoorbotHistoryEvent>();
                _cachedHistoryStart = startDate;
                _cachedHistoryEnd = endDate;
                return _cachedHistoryEvents;
            }
            finally
            {
                _historyCacheLock.Release();
            }
        }

        private static bool IsValidJpeg(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                if (stream.Length < 4)
                    return false;

                Span<byte> header = stackalloc byte[3];
                var read = stream.Read(header);
                // JPEG files start with the SOI marker: FF D8 FF
                return read == 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            }
            catch
            {
                return false;
            }
        }

        private bool WriteSnapshotMetadataFile(string mediaFilePath, string deviceId, long fileSizeBytes)
        {
            try
            {
                var metadata = new RingSnapshotMetadata(
                    FileName: Path.GetFileName(mediaFilePath),
                    DeviceId: deviceId,
                    CapturedAt: DateTime.Now,
                    FileSizeBytes: fileSizeBytes,
                    MediaFormat: "jpg"
                );

                var metadataPath = Path.ChangeExtension(mediaFilePath, ".json");
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(metadataPath, json);

                if (!ValidateJsonSidecar(metadataPath, "snapshot metadata"))
                {
                    _logger.LogWarning("Validation failed for metadata sidecar {MetadataPath}", metadataPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write metadata file for {MediaFilePath}", mediaFilePath);
                return false;
            }
        }

        /// <summary>
        /// Metadata for a device's latest-snapshot download. Unlike video metadata, this isn't tied
        /// to a specific historical event — CapturedAt reflects when we fetched it, not necessarily
        /// when Ring's camera captured the underlying image (Ring doesn't expose that timestamp here).
        /// </summary>
        private record RingSnapshotMetadata(
            string FileName,
            string DeviceId,
            DateTime CapturedAt,
            long FileSizeBytes,
            string MediaFormat
        );

        private async Task RetryWithBackoffAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken)
        {
            int delayMs = InitialDelayMs;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxRetries)
                {
                    _logger.LogWarning("Rate limit on {Operation} (attempt {Attempt}/{Max}). Waiting {DelayMs}ms before retry.",
                        operationName, attempt, MaxRetries, delayMs);

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, MaxDelayMs);
                }
            }

            // Final attempt without catch
            await operation();
        }

        private bool IsRateLimitError(Exception ex)
        {
            var message = ex.Message ?? string.Empty;

            return message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("denied by Ring", StringComparison.OrdinalIgnoreCase);
        }

        private bool ValidateJsonSidecar(string jsonPath, string metadataType)
        {
            try
            {
                // File exists and is readable
                if (!File.Exists(jsonPath))
                {
                    _logger.LogWarning("JSON sidecar file does not exist: {JsonPath}", jsonPath);
                    return false;
                }

                var fileInfo = new FileInfo(jsonPath);

                // File is not empty (minimum valid JSON is {})
                if (fileInfo.Length < 2)
                {
                    _logger.LogWarning("JSON sidecar file is too small ({Size} bytes): {JsonPath}", fileInfo.Length, jsonPath);
                    return false;
                }

                // JSON is valid
                var content = File.ReadAllText(jsonPath);
                using (var doc = JsonDocument.Parse(content))
                {
                    // If we can parse it and get the root element, it's valid JSON
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        _logger.LogInformation("✓ Validated {MetadataType} sidecar: {JsonPath} ({Size} bytes)",
                            metadataType, Path.GetFileName(jsonPath), fileInfo.Length);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("JSON sidecar root is not an object: {JsonPath}", jsonPath);
                        return false;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in sidecar: {JsonPath}", jsonPath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate JSON sidecar: {JsonPath}", jsonPath);
                return false;
            }
        }
    }
}
