using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;

[assembly: InternalsVisibleTo("VideoForensics.Providers.Ring.Tests")]

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
        // Configurable via SetMaxConcurrentDownloads (backed by IForensicsConfiguration.MaxConcurrentDownloads)
        // since Ring's per-account (not just per-connection) throttling means the right value varies
        // by account; 10 is a reasonable default.
        private int _maxConcurrentFileDownloads = 10;

        public void SetMaxConcurrentDownloads(int value)
        {
            if (value > 0)
            {
                _maxConcurrentFileDownloads = value;
            }
        }

        // Guards _currentStatus and the per-device counters below while multiple file downloads for
        // the same device update them concurrently.
        private readonly object _statusLock = new();

        // How many files are actively being fetched from Ring right now (as opposed to skipped
        // because they already exist on disk) — surfaced via DownloadStatus.ActiveConnections.
        private int _activeDownloads;

        private static void InterlockedMax(ref int target, int candidate)
        {
            int initial;
            do
            {
                initial = Volatile.Read(ref target);
                if (candidate <= initial)
                {
                    return;
                }
            } while (Interlocked.CompareExchange(ref target, candidate, initial) != initial);
        }

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

        // Transfer rate tracking: rolling average over 5-second window
        private DateTime _rateWindowStart = DateTime.UtcNow;
        private long _rateWindowBytes = 0;

        // Ring only exposes battery/connectivity health on the device-list response (ring_devices),
        // never on history/ding events (see DoorbotHistoryEvent.Doorbot's doc comment) - so capturing
        // it means its own fetch, cached like GetHistoryEventsAsync's cache above so a sequential
        // per-device download loop doesn't re-fetch the whole account's device list for every device.
        private static readonly TimeSpan HealthCacheTtl = TimeSpan.FromSeconds(30);
        private readonly SemaphoreSlim _healthCacheLock = new(1, 1);
        private Entities.Devices? _cachedHealthDevices;
        private DateTime _cachedHealthDevicesAt;

        // Thread-safe atomic increment for peak active downloads (diagnostic tracking)
        private int _peakActiveDownloadsForBatch;

        public RingMediaDownloadService(ILogger logger, ISessionProvider sessionProvider, IVideoForensicsDataClient dataClient)
        {
            _logger = logger;
            _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
            _dataClient = dataClient ?? throw new ArgumentNullException(nameof(dataClient));
        }

        public async Task<int> GetMatchedEventCountAsync(string deviceId, DateTime startDate, DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var session = _sessionProvider.GetSession();
            if (session == null)
            {
                return 0;
            }

            // Reuses GetHistoryEventsAsync's per-(startDate,endDate) cache, so when this device's
            // resolved range matches a range already fetched (e.g. another device with the same
            // watermark), no extra API call happens here.
            var events = await GetHistoryEventsAsync(session, startDate, endDate);
            return events.Count(e => e.Doorbot?.Id.ToString() == deviceId);
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
                var rateLimited = false;
                var otherItemFailures = 0;

                _activeDownloads = 0;
                _peakActiveDownloadsForBatch = 0;
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = relevantEvents.Count, FilesCompleted = 0, BytesDownloaded = 0, ActiveConnections = 0 };

                // Resolve and cache the device once at batch start to avoid per-item lookups.
                // This ensures all watermark updates within the batch operate on consistent device state.
                var deviceGuid = await EnsureDeviceIdentityAsync(deviceId, relevantEvents.FirstOrDefault()?.Doorbot?.Description ?? deviceId, cancellationToken);

                // Capture battery/connectivity telemetry once per batch (not once per event -
                // it's the same reading for the whole run). Never fails the actual video download;
                // this is purely so gap-analysis can later explain a gap as "battery was low" with
                // real data instead of guessing.
                await CaptureDeviceHealthSnapshotAsync(session, deviceId, deviceGuid, cancellationToken);

                // Cancels the in-flight batch either on real caller cancellation or the moment any
                // one file hits a rate limit — matching the previous sequential loop's "stop on
                // first rate limit" behavior, but without blocking files already in flight.
                using var rateLimitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                using var concurrencySemaphore = new SemaphoreSlim(_maxConcurrentFileDownloads, _maxConcurrentFileDownloads);

                try
                {
                    var downloadTasks = relevantEvents.Select(async (@event) =>
                    {
                        if (rateLimitCts.Token.IsCancellationRequested)
                            return;

                        var cameraName = @event.Doorbot?.Description ?? deviceId;
                        var eventType = @event.Kind ?? "video";
                        var fileName = Path.Combine(outputPath,
                            MediaFileNamer.FormatMediaFileName(cameraName, @event.CreatedAtDateTime ?? DateTime.UtcNow, eventType, "mp4"));
                        var eventIdStr = @event.Id?.ToString() ?? "unknown";
                        var eventOccurredAtUtc = (@event.CreatedAtDateTime ?? DateTime.UtcNow).ToUniversalTime();
                        var previousAttemptCount = 0;

                        // Record this event in the Events table independent of download outcome —
                        // Timeline/Integrity/Correlation/Audit forensic tools all read from Events,
                        // not DownloadEvents, so an event must land here the moment it's discovered
                        // (not only once/if it's successfully downloaded) or gaps and missing-download
                        // detection have nothing to compare against.
                        await UpsertEventRecordAsync(deviceGuid, eventIdStr, eventType, eventOccurredAtUtc,
                            @event.SnapshotUrl, downloadedAtUtc: null, hash: null, rateLimitCts.Token, apiResponse: @event);

                        // Use the device GUID resolved at batch start; all events are for this same device
                        // (filtered by deviceId above). Avoids per-item redundant lookups.

                        // Everything below runs per-item with explicit concurrency control. An unhandled
                        // exception here (e.g. a bad DB round-trip or a filesystem error on the "already
                        // downloaded" check) is wrapped so a single bad item can only fail itself.
                        try
                        {
                            // Check both filesystem and DB for existing download. The DB flag alone is not
                            // trustworthy — it can be true while the file itself is missing (deleted, moved,
                            // or downloaded to a different output path in a prior run). Only skip the
                            // network call when the file is actually present on disk; otherwise fall through
                            // and redownload it, even though the DB says it was already downloaded.
                            var existingRecord = await _dataClient.GetDownloadEventAsync(deviceGuid, eventIdStr, rateLimitCts.Token);
                            var existsOnDisk = File.Exists(fileName) && new FileInfo(fileName).Length > 0;
                            previousAttemptCount = existingRecord?.AttemptCount ?? 0;

                            // A record with Success=false and no attempt limit reached means a prior run
                            // hit a permanent failure (e.g. Ring reports the recording no longer exists —
                            // DeviceUnknownException/HTTP 404) rather than a transient one. Retrying an item
                            // that will never succeed just burns an API call and reappears in "N more matched
                            // but not downloaded" forever. Cap retries instead of never retrying at all, in
                            // case the recording becomes available again later or the failure was transient.
                            if (existingRecord is { Success: false, AttemptCount: >= MaxRetries } && !existsOnDisk)
                            {
                                _activityLog.Enqueue($"[dim]⊘ {EscapeMarkup(Path.GetFileName(fileName))}: skipped, failed permanently after {existingRecord.AttemptCount} attempt(s) ({EscapeMarkup(existingRecord.ErrorMessage ?? "unknown error")})[/]");
                                return;
                            }

                            if (existingRecord != null && !existsOnDisk)
                            {
                                _logger.LogInformation("Event {EventId} is marked downloaded in the database but {FileName} is missing on disk; redownloading", eventIdStr, fileName);
                            }

                            if (existsOnDisk)
                            {
                                var existingSize = new FileInfo(fileName).Length;

                                // Compute hash for existing file if it exists on disk
                                string? sha256Hash = null;
                                {
                                    try
                                    {
                                        var hashBytes = await SHA256.HashDataAsync(File.OpenRead(fileName), rateLimitCts.Token);
                                        sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to compute hash for existing file {FileName}", fileName);
                                    }
                                }

                                var wroteMetadata = !File.Exists(Path.ChangeExtension(fileName, ".json")) &&
                                    WriteMetadataFile(fileName, deviceId, @event, existingSize, "mp4");

                                if (sha256Hash != null)
                                {
                                    await UpsertEventRecordAsync(deviceGuid, eventIdStr, eventType, eventOccurredAtUtc,
                                        @event.SnapshotUrl, downloadedAtUtc: DateTime.UtcNow, hash: sha256Hash, rateLimitCts.Token, apiResponse: @event);
                                }

                                lock (_statusLock)
                                {
                                    downloadedFiles++;
                                    downloadedBytes += existingSize;
                                    mediaFilesValidated++;
                                    validatedFiles.Add(fileName);
                                    if (wroteMetadata)
                                        metadataFilesWritten++;

                                    RecordBytesForRate(existingSize);

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

                                _activityLog.Enqueue($"[dim]○[/] {Path.GetFileName(fileName)} ({FormatBytes(existingSize)}) already exists");
                                return;
                            }
                        }
                        catch (OperationCanceledException) when (rateLimitCts.Token.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to check existing download for event {EventId}", @event.Id);
                            _activityLog.Enqueue($"[red]✗[/] event {@event.Id}: {EscapeMarkup(ex.Message)}");
                            return;
                        }

                        // Acquire semaphore slot before fetching from Ring
                        bool acquiredSemaphore = false;
                        try
                        {
                            await concurrencySemaphore.WaitAsync(rateLimitCts.Token);
                            acquiredSemaphore = true;
                        }
                        catch (OperationCanceledException) when (rateLimitCts.Token.IsCancellationRequested)
                        {
                            _activityLog.Enqueue($"[yellow]⊘[/] {Path.GetFileName(fileName)} cancelled");
                            return;
                        }

                        try
                        {
                            var concurrentNow = Interlocked.Increment(ref _activeDownloads);
                            InterlockedMax(ref _peakActiveDownloadsForBatch, concurrentNow);
                            try
                            {
                                await RetryWithBackoffAsync(async () =>
                                {
                                    await session.GetDoorbotHistoryRecording(@event, fileName);
                                }, $"video for event {@event.Id}", rateLimitCts.Token);
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
                                        var hashBytes = await SHA256.HashDataAsync(File.OpenRead(fileName), rateLimitCts.Token);
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

                                        var metadata = SerializeMetadata(@event);
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
                                            IntegrityVerified = false,
                                            MetadataJson = metadata.Json,
                                            ApiSourceHash = metadata.Hash
                                        };

                                        await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, rateLimitCts.Token);

                                        await UpsertEventRecordAsync(deviceGuid, eventIdStr, eventType, eventOccurredAtUtc,
                                            @event.SnapshotUrl, downloadedAtUtc: DateTime.UtcNow, hash: sha256Hash, rateLimitCts.Token, apiResponse: @event);

                                        // Update watermark after successful DownloadEvent recording using the authoritative EventOccurredAtUtc
                                        try
                                        {
                                            await _dataClient.UpdateDeviceWatermarkAsync(deviceGuid, downloadEvent.EventOccurredAtUtc, rateLimitCts.Token);
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

                                    RecordBytesForRate(downloadedSize);

                                    _currentStatus = _currentStatus with
                                    {
                                        FilesCompleted = downloadedFiles,
                                        BytesDownloaded = downloadedBytes,
                                        CurrentFile = fileName,
                                        ActiveConnections = Volatile.Read(ref _activeDownloads)
                                    };
                                }

                                _activityLog.Enqueue($"[green]✓[/] {Path.GetFileName(fileName)} ({FormatBytes(downloadedSize)})");
                            }
                            else
                            {
                                _activityLog.Enqueue($"[red]✗[/] {Path.GetFileName(fileName)} empty/invalid");
                            }
                        }
                        catch (OperationCanceledException) when (rateLimitCts.Token.IsCancellationRequested)
                        {
                            _activityLog.Enqueue($"[yellow]⊘[/] {Path.GetFileName(fileName)} cancelled");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to download video for event {EventId}", @event.Id);
                            _activityLog.Enqueue($"[red]✗ {EscapeMarkup(Path.GetFileName(fileName))}: {EscapeMarkup(HumanizeExceptionTypeName(ex.GetType().Name))}[/]");
                            if (IsRateLimitError(ex))
                            {
                                _logger.LogInformation("Rate limit detected. Stopping remaining downloads for this device.");
                                rateLimited = true;
                                rateLimitCts.Cancel();
                            }
                            else
                            {
                                Interlocked.Increment(ref otherItemFailures);

                                // Record the failed attempt so a subsequent run/"Continue downloading"
                                // can tell this item was already tried (see the MaxRetries skip check
                                // above) instead of hitting the Ring API again for something that will
                                // never succeed - e.g. DeviceUnknownException (HTTP 404), which means
                                // Ring itself no longer has this recording.
                                try
                                {
                                    var failedEvent = new DownloadEvent
                                    {
                                        Id = Guid.NewGuid(),
                                        DeviceId = deviceGuid,
                                        ProviderEventId = eventIdStr,
                                        EventType = @event.Kind,
                                        Answered = @event.Answered,
                                        Favorite = @event.Favorite,
                                        EventOccurredAtUtc = eventOccurredAtUtc,
                                        RecordingStatus = @event.Recording?.Status,
                                        DownloadStartedUtc = DateTime.UtcNow,
                                        DownloadCompletedUtc = DateTime.UtcNow,
                                        Success = false,
                                        AttemptCount = previousAttemptCount + 1,
                                        ErrorMessage = HumanizeExceptionTypeName(ex.GetType().Name) + ": " + ex.Message,
                                        AppVersion = typeof(RingMediaDownloadService).Assembly.GetName().Version?.ToString() ?? "unknown"
                                    };
                                    await _dataClient.RecordDownloadEventAsync(failedEvent, media: null, CancellationToken.None);
                                }
                                catch (Exception recordEx)
                                {
                                    _logger.LogWarning(recordEx, "Failed to record failed-download attempt in database for event {EventId}", eventIdStr);
                                }
                            }
                        }
                        finally
                        {
                            if (acquiredSemaphore)
                            {
                                concurrencySemaphore.Release();
                            }
                        }
                    });

                    await Task.WhenAll(downloadTasks);
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
                _logger.LogInformation("[DIAG] Peak concurrent downloads this batch: {Peak} (configured max: {Max}, items in batch: {ItemCount})",
                    _peakActiveDownloadsForBatch, _maxConcurrentFileDownloads, relevantEvents.Count);
                // _logger has no provider registered in Program.cs (nothing sinks LogInformation
                // anywhere right now), so also surface this via the activity log the UI already drains.
                _activityLog.Enqueue($"[grey][[DIAG]][/] Peak concurrent downloads: {_peakActiveDownloadsForBatch} (configured max: {_maxConcurrentFileDownloads}, items: {relevantEvents.Count})");

                // Update device watermark to batch end time to prevent re-scanning the last hour
                // on the next download (without this, the 1-hour watermark buffer causes regression).
                // Only update if we actually scanned events for this device.
                if (relevantEvents.Count > 0 && downloadedFiles > 0)
                {
                    try
                    {
                        await _dataClient.UpdateDeviceWatermarkAsync(deviceGuid, endDate, CancellationToken.None);
                        _logger.LogInformation("Advanced device watermark to batch end time {EndDate} to prevent re-scanning", endDate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update device watermark to batch end time; next download may re-process some events");
                    }
                }

                string? skipReason = null;
                if (downloadedFiles < relevantEvents.Count)
                {
                    if (rateLimited)
                    {
                        skipReason = "rate limited by Ring API";
                    }
                    else if (cancellationToken.IsCancellationRequested)
                    {
                        skipReason = "cancelled";
                    }
                    else if (otherItemFailures > 0)
                    {
                        skipReason = otherItemFailures == 1 ? "1 item failed to download" : $"{otherItemFailures} items failed to download";
                    }
                }

                return new DownloadResult(
                    Success: true,
                    FilesDownloaded: downloadedFiles,
                    BytesDownloaded: downloadedBytes,
                    MetadataFilesWritten: metadataFilesWritten,
                    MediaFilesValidated: mediaFilesValidated,
                    ValidatedFiles: validatedFiles,
                    FilesMatched: relevantEvents.Count,
                    SkipReason: skipReason
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

                // Get device name for consistent file naming (fallback to deviceId if unavailable)
                var deviceNameForSnapshot = deviceId; // Snapshots don't have event data with device name, so use deviceId
                var fileName = Path.Combine(outputPath, MediaFileNamer.FormatMediaFileName(deviceNameForSnapshot, DateTime.UtcNow, "snapshot", "jpg"));

                await RetryWithBackoffAsync(async () =>
                {
                    await session.GetLatestSnapshot(doorbotId, fileName);
                }, $"latest snapshot for device {deviceId}", cancellationToken);

                if (!File.Exists(fileName))
                {
                    _currentStatus = _currentStatus with { IsDownloading = false };
                    _activityLog.Enqueue($"[red]✗[/] {deviceNameForSnapshot}: no snapshot available");
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
                    _activityLog.Enqueue($"[red]✗[/] {deviceNameForSnapshot}: offline/no snapshot");
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

                        var snapshotMetadata = SerializeMetadata(new { eventId = snapshotEventId, type = "snapshot", deviceId = deviceId, timestamp = DateTime.UtcNow });
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
                            IntegrityVerified = false,
                            MetadataJson = snapshotMetadata.Json,
                            ApiSourceHash = snapshotMetadata.Hash
                        };

                        await _dataClient.RecordDownloadEventAsync(downloadEvent, mediaItem, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record snapshot download event in database for {FileName}. Download succeeded but database record was not created.", fileName);
                    }
                }

                lock (_statusLock)
                {
                    RecordBytesForRate(fileSize);
                    _currentStatus = _currentStatus with
                    {
                        IsDownloading = false,
                        FilesCompleted = 1,
                        BytesDownloaded = fileSize,
                        CurrentFile = fileName
                    };
                }

                _activityLog.Enqueue($"[green]✓[/] {Path.GetFileName(fileName)} ({FormatBytes(fileSize)})");

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

        /// <summary>
        /// Upserts an Events-table record for a provider event, independent of download outcome.
        /// Called once per event at discovery (downloadedAtUtc/hash null) and again once the event
        /// is successfully downloaded and hashed, to progressively enrich the same row. Failures here
        /// are logged and swallowed — a bad Events write shouldn't fail the download itself.
        /// </summary>
        /// <summary>Serializes an API response object to JSON and computes its SHA256 hash for audit/change tracking.</summary>
        private (string Json, string Hash) SerializeMetadata(object? apiResponse)
        {
            if (apiResponse == null)
                return (string.Empty, string.Empty);

            using var hash = SHA256.Create();
            var json = JsonSerializer.Serialize(apiResponse);
            var hashValue = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            var hashHex = Convert.ToHexString(hashValue);
            return (json, hashHex);
        }

        private async Task UpsertEventRecordAsync(Guid deviceGuid, string providerEventId, string eventType,
            DateTime occurredAtUtc, string? snapshotUrl, DateTime? downloadedAtUtc, string? hash, CancellationToken ct,
            object? apiResponse = null)
        {
            try
            {
                var metadata = SerializeMetadata(apiResponse);
                await _dataClient.UpsertEventAsync(new Event
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceGuid,
                    ProviderEventId = providerEventId,
                    EventType = eventType,
                    OccurredAtUtc = occurredAtUtc,
                    SnapshotUrl = snapshotUrl,
                    DiscoveredAtUtc = DateTime.UtcNow,
                    DownloadedAtUtc = downloadedAtUtc,
                    EventIntegrityHash = hash,
                    MetadataJson = metadata.Json,
                    ApiSourceHash = metadata.Hash
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert Events record for event {ProviderEventId}", providerEventId);
            }
        }

        public DownloadStatus GetStatus()
        {
            lock (_statusLock)
            {
                return _currentStatus;
            }
        }

        // Track bytes for 5-second rolling average transfer rate
        private void RecordBytesForRate(long bytes)
        {
            lock (_statusLock)
            {
                var now = DateTime.UtcNow;
                var elapsedSeconds = (now - _rateWindowStart).TotalSeconds;

                // Reset window if it's been 5+ seconds
                if (elapsedSeconds >= 5.0)
                {
                    _rateWindowStart = now;
                    _rateWindowBytes = bytes;
                }
                else
                {
                    _rateWindowBytes += bytes;
                }

                // Calculate current rate in MB/s
                var rateMbps = elapsedSeconds > 0 ? (_rateWindowBytes / (1024.0 * 1024.0)) / elapsedSeconds : 0.0;
                _currentStatus = _currentStatus with { CurrentSpeedMbps = rateMbps };
            }
        }

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

        /// <summary>Turns a PascalCase exception type name (e.g. "DeviceUnknownException") into a
        /// readable, space-separated form ("Device Unknown Exception") for display in the activity log.</summary>
        private static string HumanizeExceptionTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeName;

            var sb = new System.Text.StringBuilder(typeName.Length + 8);
            for (var i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(typeName[i]);
            }
            return sb.ToString();
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
                    LocationId: @event.Doorbot?.LocationId,
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
            Guid? LocationId,
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
                        ct: ct);
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
                ct: ct);

            // Cache in the per-device dictionary
            _deviceIdCache.TryAdd(providerDeviceId, device.Id);
            return device.Id;
        }

        /// <summary>
        /// Fetches this device's current battery/connectivity telemetry and persists it as a
        /// DeviceHealthSnapshot, so gap-analysis can later explain a recording gap with real data
        /// ("battery was at 8% shortly before this gap began") instead of guessing. Best-effort:
        /// any failure here is logged and swallowed, never fails the underlying video download.
        /// </summary>
        private async Task CaptureDeviceHealthSnapshotAsync(Session session, string providerDeviceId, Guid deviceGuid, CancellationToken ct)
        {
            try
            {
                var devices = await GetDevicesForHealthAsync(session, ct);
                var health = FindDeviceHealth(devices, providerDeviceId);
                if (health == null)
                {
                    _logger.LogDebug("No health telemetry available for device {DeviceId} in this run", providerDeviceId);
                    return;
                }

                var snapshot = new DeviceHealthSnapshot
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceGuid,
                    Connected = health.Connected,
                    BatteryPercentage = health.BatteryPercentage.HasValue ? (decimal)health.BatteryPercentage.Value : null,
                    Rssi = health.Rssi.HasValue ? (int)Math.Round(health.Rssi.Value) : null,
                    WifiName = health.WifiName,
                    FirmwareVersion = health.FirmwareVersion,
                    CapturedAtUtc = DateTime.UtcNow
                };

                await _dataClient.RecordDeviceHealthSnapshotAsync(snapshot, ct);
                _logger.LogInformation("Captured health snapshot for device {DeviceId}: battery={Battery}%, connected={Connected}, rssi={Rssi}",
                    providerDeviceId, snapshot.BatteryPercentage, snapshot.Connected, snapshot.Rssi);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture device health snapshot for device {DeviceId} (non-critical)", providerDeviceId);
            }
        }

        // internal (not private) so RingMediaDownloadServiceTests can verify the matching logic
        // directly - Session makes real HTTP calls and isn't mockable, so this is the only part of
        // CaptureDeviceHealthSnapshotAsync that's unit-testable without a live Ring account.
        internal static Entities.DeviceHealth? FindDeviceHealth(Entities.Devices? devices, string providerDeviceId)
        {
            if (devices == null)
            {
                return null;
            }

            var doorbot = devices.Doorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId);
            if (doorbot?.Health != null)
            {
                return doorbot.Health;
            }

            var stickupCam = devices.StickupCams?.FirstOrDefault(d => d.Id.HasValue && d.Id.Value.ToString() == providerDeviceId);
            if (stickupCam?.Health != null)
            {
                return stickupCam.Health;
            }

            var authorizedDoorbot = devices.AuthorizedDoorbots?.FirstOrDefault(d => d.Id.ToString() == providerDeviceId);
            return authorizedDoorbot?.Health;
        }

        private async Task<Entities.Devices?> GetDevicesForHealthAsync(Session session, CancellationToken ct)
        {
            if (_cachedHealthDevices != null && DateTime.UtcNow - _cachedHealthDevicesAt < HealthCacheTtl)
            {
                return _cachedHealthDevices;
            }

            await _healthCacheLock.WaitAsync(ct);
            try
            {
                if (_cachedHealthDevices != null && DateTime.UtcNow - _cachedHealthDevicesAt < HealthCacheTtl)
                {
                    return _cachedHealthDevices;
                }

                var devices = await session.GetRingDevices();
                _cachedHealthDevices = devices;
                _cachedHealthDevicesAt = DateTime.UtcNow;
                return devices;
            }
            finally
            {
                _healthCacheLock.Release();
            }
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
