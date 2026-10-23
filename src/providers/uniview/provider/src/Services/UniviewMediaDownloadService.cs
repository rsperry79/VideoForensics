using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Providers.Uniview.Services;

/// <summary>
/// Uniview media download service. Handles video and snapshot downloads from Uniview NVR devices.
/// Implements the month-chunking pattern required by the API (see docs/NVR_API.md section 9).
/// Thread-safe for concurrent status reads and activity log drains while downloads happen.
/// </summary>
public class UniviewMediaDownloadService : IMediaDownloadService
{
    private const string ProviderName = "Uniview";

    private readonly ILogger<UniviewMediaDownloadService> _logger;
    private readonly IUniviewSessionProvider _sessionProvider;
    private readonly Func<UniviewClient, int, string, CancellationToken, Task> _liveSnapshotCapture;

    // Mutable state tracking — accessed from download thread and status-query threads
    private DownloadStatus _currentStatus = new(IsDownloading: false, FilesCompleted: 0, FilesTotal: 0, BytesDownloaded: 0);
    private readonly object _statusLock = new();

    // Per-item outcomes queued for draining by UI
    private readonly ConcurrentQueue<string> _activityLog = new();

    // Capacity exception ban tracking: device-side concurrency limit (code 60031)
    // Not time-based like cloud rate limits, but treat as "banned for N minutes"
    private DateTime? _capacityBanUntilUtc;
    private const int CapacityBanMinutes = 5;

    /// <param name="liveSnapshotCapture">
    /// Grabs one live frame to a JPEG path. Defaults to the client's RTSP grab; tests pass a fake so the fallback
    /// runs without a camera or ffmpeg.
    /// </param>
    public UniviewMediaDownloadService(
        ILogger<UniviewMediaDownloadService> logger,
        IUniviewSessionProvider sessionProvider,
        Func<UniviewClient, int, string, CancellationToken, Task>? liveSnapshotCapture = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
        _liveSnapshotCapture = liveSnapshotCapture ?? ((client, channel, path, ct) => client.CaptureLiveSnapshotAsync(channel, path, ct));
    }

    /// <summary>
    /// Downloads videos for a device within a date range. Splits the range into month-sized chunks
    /// per the "query one month at a time" requirement documented in docs/NVR_API.md section 9.
    /// Account-aware overload that retrieves the client for a specific provider account.
    /// </summary>
    public async Task<DownloadResult> DownloadVideosAsync(
        Guid providerAccountId,
        string deviceId,
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        string? providerLocationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading videos for {ProviderName} account, device {DeviceId} from {StartDate} to {EndDate}",
                ProviderName, deviceId, startDate, endDate);

            UniviewClient? client = _sessionProvider.GetClient(providerAccountId);
            if (client is null)
            {
                _logger.LogWarning("Not authenticated: Client is null for {ProviderName} account", ProviderName);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number (expect numeric or "channel:<num>" format)
            if (!TryParseChannelNumber(deviceId, out int channel))
            {
                _logger.LogWarning("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            // Split date range into month-sized chunks and query each
            var allSegments = new List<RecordSegment>();
            List<(DateTimeOffset Start, DateTimeOffset End)> monthChunks = GetMonthChunks(startDate, endDate);

            foreach ((DateTimeOffset monthStart, DateTimeOffset monthEnd) in monthChunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    IReadOnlyList<RecordSegment> monthSegments = await client.ListSegmentsAsync(channel, monthStart, monthEnd, cancellationToken);
                    allSegments.AddRange(monthSegments);

                    // Log warning if a single month's query looks like it hit the 2000-result cap
                    if (monthSegments.Count >= 2000)
                    {
                        _logger.LogWarning(
                            "Month {YearMonth} returned exactly 2000 or more segments for channel {Channel} — " +
                            "may have hit per-query result cap; not attempting to work around it further",
                            monthStart.ToString("yyyy-MM"), channel);
                        _activityLog.Enqueue(
                            $"⚠ Month {monthStart:yyyy-MM} returned {monthSegments.Count} segments (possible result cap hit)");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to list segments for month {Month}, channel {Channel}",
                        monthStart.ToString("yyyy-MM"), channel);
                    _activityLog.Enqueue($"✗ Failed to query {monthStart:yyyy-MM}: {ex.Message}");
                    // Continue to next month rather than failing the whole batch
                }
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with
                {
                    IsDownloading = true,
                    FilesTotal = allSegments.Count,
                    FilesCompleted = 0,
                    BytesDownloaded = 0
                };
            }

            _logger.LogDebug("Found {SegmentCount} segments across all months for channel {Channel}",
                allSegments.Count, channel);

            int filesDownloaded = 0;
            long bytesDownloaded = 0L;

            // Download each segment sequentially
            foreach (RecordSegment segment in allSegments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string fileName = EnsureWithinRoot(outputPath, BuildSegmentFileName(outputPath, deviceId, segment));

                try
                {
                    // Skip if already exists
                    if (File.Exists(fileName) && new FileInfo(fileName).Length > 0)
                    {
                        long existingSize = new FileInfo(fileName).Length;
                        lock (_statusLock)
                        {
                            filesDownloaded++;
                            bytesDownloaded += existingSize;
                            _currentStatus = _currentStatus with
                            {
                                FilesCompleted = filesDownloaded,
                                BytesDownloaded = bytesDownloaded,
                                CurrentFile = fileName
                            };
                        }

                        _activityLog.Enqueue($"○ {Path.GetFileName(fileName)} ({FormatBytes(existingSize)}) already exists");
                        continue;
                    }

                    await client.DownloadSegmentAsync(segment, fileName, cancellationToken);

                    if (!File.Exists(fileName))
                    {
                        _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: download produced no file");
                        continue;
                    }

                    long fileSize = new FileInfo(fileName).Length;
                    if (fileSize == 0)
                    {
                        _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: empty file");
                        File.Delete(fileName);
                        continue;
                    }

                    lock (_statusLock)
                    {
                        filesDownloaded++;
                        bytesDownloaded += fileSize;
                        _currentStatus = _currentStatus with
                        {
                            FilesCompleted = filesDownloaded,
                            BytesDownloaded = bytesDownloaded,
                            CurrentFile = fileName
                        };
                    }

                    _activityLog.Enqueue($"✓ {Path.GetFileName(fileName)} ({FormatBytes(fileSize)})");
                }
                catch (DownloadCapacityException ex)
                {
                    _logger.LogError(ex, "Device capacity limit hit while downloading segment; stopping batch");
                    lock (_statusLock)
                    {
                        _capacityBanUntilUtc = DateTime.UtcNow.AddMinutes(CapacityBanMinutes);
                    }

                    _activityLog.Enqueue($"✗ Capacity limit (code 60031): {ex.Message}");

                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    return new DownloadResult(
                        Success: false,
                        FilesDownloaded: filesDownloaded,
                        BytesDownloaded: bytesDownloaded,
                        ErrorMessage: "Download stopped: device capacity limit reached. " +
                                      "Wait for stale sessions to expire or reboot the NVR.",
                        FilesMatched: allSegments.Count);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download segment {Begin:O} for channel {Channel}",
                        segment.Begin, channel);
                    _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: {ex.GetType().Name}");
                    // Continue with next segment rather than failing the whole batch
                }
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            _logger.LogInformation("Downloaded {FileCount} videos ({Bytes} bytes) for device {DeviceId}",
                filesDownloaded, bytesDownloaded, deviceId);

            return new DownloadResult(
                Success: true,
                FilesDownloaded: filesDownloaded,
                BytesDownloaded: bytesDownloaded,
                FilesMatched: allSegments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading videos for {ProviderName} account, device {DeviceId}", ProviderName, deviceId);
            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads videos for a device within a date range. Splits the range into month-sized chunks
    /// per the "query one month at a time" requirement documented in docs/NVR_API.md section 9.
    /// </summary>
    public async Task<DownloadResult> DownloadVideosAsync(
        string deviceId,
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        string? providerLocationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading videos for device {DeviceId} from {StartDate} to {EndDate}",
                deviceId, startDate, endDate);

            UniviewClient? client = _sessionProvider.GetClient();
            if (client is null)
            {
                _logger.LogWarning("Not authenticated: Client is null");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number (expect numeric or "channel:<num>" format)
            if (!TryParseChannelNumber(deviceId, out int channel))
            {
                _logger.LogWarning("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            // Split date range into month-sized chunks and query each
            var allSegments = new List<RecordSegment>();
            List<(DateTimeOffset Start, DateTimeOffset End)> monthChunks = GetMonthChunks(startDate, endDate);

            foreach ((DateTimeOffset monthStart, DateTimeOffset monthEnd) in monthChunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    IReadOnlyList<RecordSegment> monthSegments = await client.ListSegmentsAsync(channel, monthStart, monthEnd, cancellationToken);
                    allSegments.AddRange(monthSegments);

                    // Log warning if a single month's query looks like it hit the 2000-result cap
                    if (monthSegments.Count >= 2000)
                    {
                        _logger.LogWarning(
                            "Month {YearMonth} returned exactly 2000 or more segments for channel {Channel} — " +
                            "may have hit per-query result cap; not attempting to work around it further",
                            monthStart.ToString("yyyy-MM"), channel);
                        _activityLog.Enqueue(
                            $"⚠ Month {monthStart:yyyy-MM} returned {monthSegments.Count} segments (possible result cap hit)");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to list segments for month {Month}, channel {Channel}",
                        monthStart.ToString("yyyy-MM"), channel);
                    _activityLog.Enqueue($"✗ Failed to query {monthStart:yyyy-MM}: {ex.Message}");
                    // Continue to next month rather than failing the whole batch
                }
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with
                {
                    IsDownloading = true,
                    FilesTotal = allSegments.Count,
                    FilesCompleted = 0,
                    BytesDownloaded = 0
                };
            }

            _logger.LogDebug("Found {SegmentCount} segments across all months for channel {Channel}",
                allSegments.Count, channel);

            int filesDownloaded = 0;
            long bytesDownloaded = 0L;

            // Download each segment sequentially
            foreach (RecordSegment segment in allSegments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string fileName = EnsureWithinRoot(outputPath, BuildSegmentFileName(outputPath, deviceId, segment));

                try
                {
                    // Skip if already exists
                    if (File.Exists(fileName) && new FileInfo(fileName).Length > 0)
                    {
                        long existingSize = new FileInfo(fileName).Length;
                        lock (_statusLock)
                        {
                            filesDownloaded++;
                            bytesDownloaded += existingSize;
                            _currentStatus = _currentStatus with
                            {
                                FilesCompleted = filesDownloaded,
                                BytesDownloaded = bytesDownloaded,
                                CurrentFile = fileName
                            };
                        }

                        _activityLog.Enqueue($"○ {Path.GetFileName(fileName)} ({FormatBytes(existingSize)}) already exists");
                        continue;
                    }

                    await client.DownloadSegmentAsync(segment, fileName, cancellationToken);

                    if (!File.Exists(fileName))
                    {
                        _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: download produced no file");
                        continue;
                    }

                    long fileSize = new FileInfo(fileName).Length;
                    if (fileSize == 0)
                    {
                        _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: empty file");
                        File.Delete(fileName);
                        continue;
                    }

                    lock (_statusLock)
                    {
                        filesDownloaded++;
                        bytesDownloaded += fileSize;
                        _currentStatus = _currentStatus with
                        {
                            FilesCompleted = filesDownloaded,
                            BytesDownloaded = bytesDownloaded,
                            CurrentFile = fileName
                        };
                    }

                    _activityLog.Enqueue($"✓ {Path.GetFileName(fileName)} ({FormatBytes(fileSize)})");
                }
                catch (DownloadCapacityException ex)
                {
                    _logger.LogError(ex, "Device capacity limit hit while downloading segment; stopping batch");
                    lock (_statusLock)
                    {
                        _capacityBanUntilUtc = DateTime.UtcNow.AddMinutes(CapacityBanMinutes);
                    }

                    _activityLog.Enqueue($"✗ Capacity limit (code 60031): {ex.Message}");

                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    return new DownloadResult(
                        Success: false,
                        FilesDownloaded: filesDownloaded,
                        BytesDownloaded: bytesDownloaded,
                        ErrorMessage: "Download stopped: device capacity limit reached. " +
                                      "Wait for stale sessions to expire or reboot the NVR.",
                        FilesMatched: allSegments.Count);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download segment {Begin:O} for channel {Channel}",
                        segment.Begin, channel);
                    _activityLog.Enqueue($"✗ {Path.GetFileName(fileName)}: {ex.GetType().Name}");
                    // Continue with next segment rather than failing the whole batch
                }
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            _logger.LogInformation("Downloaded {FileCount} videos ({Bytes} bytes) for device {DeviceId}",
                filesDownloaded, bytesDownloaded, deviceId);

            return new DownloadResult(
                Success: true,
                FilesDownloaded: filesDownloaded,
                BytesDownloaded: bytesDownloaded,
                FilesMatched: allSegments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading videos for device {DeviceId}", deviceId);
            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// With a valid from/to window, extracts one JPEG per recorded segment in that window (device has no
    /// snapshot API, so frames come from the downloaded recording). Without a window, falls back to a
    /// single live RTSP frame grab.
    /// Account-aware overload that retrieves the client for a specific provider account.
    /// </summary>
    public async Task<DownloadResult> DownloadSnapshotsAsync(
        Guid providerAccountId,
        string deviceId,
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        string? providerLocationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Requesting live snapshot for {ProviderName} account, device {DeviceId}", ProviderName, deviceId);

            UniviewClient? client = _sessionProvider.GetClient(providerAccountId);
            if (client is null)
            {
                _logger.LogWarning("Not authenticated: Client is null for {ProviderName} account", ProviderName);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number
            if (!TryParseChannelNumber(deviceId, out int channel))
            {
                _logger.LogWarning("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            _ = Directory.CreateDirectory(outputPath);

            // A real from/to window means snapshots come from recorded footage in that window;
            // the live frame below is only the fallback when no window was supplied.
            if (HasRequestedWindow(startDate, endDate))
            {
                return await DownloadWindowedSnapshotsAsync(client, channel, deviceId, outputPath, startDate, endDate, cancellationToken);
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = 1, FilesCompleted = 0 };
            }

            string fileName = EnsureWithinRoot(outputPath, Path.Combine(outputPath, $"snapshot_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg"));

            try
            {
                await _liveSnapshotCapture(client, channel, fileName, cancellationToken);

                if (!File.Exists(fileName))
                {
                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    _activityLog.Enqueue("✗ Snapshot: ffmpeg failed to produce a file");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Snapshot capture failed: ffmpeg produced no output");
                }

                long fileSize = new FileInfo(fileName).Length;
                if (fileSize == 0)
                {
                    File.Delete(fileName);
                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    _activityLog.Enqueue("✗ Snapshot: empty file");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Snapshot capture failed: empty file");
                }

                lock (_statusLock)
                {
                    _currentStatus = _currentStatus with
                    {
                        IsDownloading = false,
                        FilesCompleted = 1,
                        BytesDownloaded = fileSize,
                        CurrentFile = fileName
                    };
                }

                string msg = $"✓ {Path.GetFileName(fileName)} ({FormatBytes(fileSize)}) — " +
                          "Live snapshot only (Uniview device has no historical snapshot API)";
                _activityLog.Enqueue(msg);

                _logger.LogDebug("Downloaded live snapshot ({Bytes} bytes) for {ProviderName} account, device {DeviceId}",
                    fileSize, ProviderName, deviceId);

                return new DownloadResult(
                    Success: true,
                    FilesDownloaded: 1,
                    BytesDownloaded: fileSize,
                    ErrorMessage: "Only a live snapshot was captured (requested date range cannot be honored " +
                                  "— this device has no historical snapshot capability)",
                    FilesMatched: 1);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing snapshot for {ProviderName} account, device {DeviceId}", ProviderName, deviceId);
                lock (_statusLock)
                {
                    _currentStatus = _currentStatus with { IsDownloading = false };
                }

                _activityLog.Enqueue($"✗ Snapshot: {ex.GetType().Name}");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Snapshot capture failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DownloadSnapshotsAsync for {ProviderName} account, device {DeviceId}", ProviderName, deviceId);
            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download failed: {ex.Message}");
        }
    }

    /// <summary>
    /// With a valid from/to window, extracts one JPEG per recorded segment in that window (device has no
    /// snapshot API, so frames come from the downloaded recording). Without a window, falls back to a
    /// single live RTSP frame grab.
    /// </summary>
    public async Task<DownloadResult> DownloadSnapshotsAsync(
        string deviceId,
        string outputPath,
        DateTime startDate,
        DateTime endDate,
        string? providerLocationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Requesting live snapshot for device {DeviceId}", deviceId);

            UniviewClient? client = _sessionProvider.GetClient();
            if (client is null)
            {
                _logger.LogWarning("Not authenticated: Client is null");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number
            if (!TryParseChannelNumber(deviceId, out int channel))
            {
                _logger.LogWarning("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            _ = Directory.CreateDirectory(outputPath);

            // A real from/to window means snapshots come from recorded footage in that window;
            // the live frame below is only the fallback when no window was supplied.
            if (HasRequestedWindow(startDate, endDate))
            {
                return await DownloadWindowedSnapshotsAsync(client, channel, deviceId, outputPath, startDate, endDate, cancellationToken);
            }

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = 1, FilesCompleted = 0 };
            }

            string fileName = EnsureWithinRoot(outputPath, Path.Combine(outputPath, $"snapshot_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg"));

            try
            {
                await _liveSnapshotCapture(client, channel, fileName, cancellationToken);

                if (!File.Exists(fileName))
                {
                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    _activityLog.Enqueue("✗ Snapshot: ffmpeg failed to produce a file");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Snapshot capture failed: ffmpeg produced no output");
                }

                long fileSize = new FileInfo(fileName).Length;
                if (fileSize == 0)
                {
                    File.Delete(fileName);
                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { IsDownloading = false };
                    }

                    _activityLog.Enqueue("✗ Snapshot: empty file");
                    return new DownloadResult(
                        Success: false,
                        ErrorMessage: "Snapshot capture failed: empty file");
                }

                lock (_statusLock)
                {
                    _currentStatus = _currentStatus with
                    {
                        IsDownloading = false,
                        FilesCompleted = 1,
                        BytesDownloaded = fileSize,
                        CurrentFile = fileName
                    };
                }

                string msg = $"✓ {Path.GetFileName(fileName)} ({FormatBytes(fileSize)}) — " +
                          "Live snapshot only (Uniview device has no historical snapshot API)";
                _activityLog.Enqueue(msg);

                _logger.LogDebug("Downloaded live snapshot ({Bytes} bytes) for device {DeviceId}",
                    fileSize, deviceId);

                return new DownloadResult(
                    Success: true,
                    FilesDownloaded: 1,
                    BytesDownloaded: fileSize,
                    ErrorMessage: "Only a live snapshot was captured (requested date range cannot be honored " +
                                  "— this device has no historical snapshot capability)",
                    FilesMatched: 1);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing snapshot for device {DeviceId}", deviceId);
                lock (_statusLock)
                {
                    _currentStatus = _currentStatus with { IsDownloading = false };
                }

                _activityLog.Enqueue($"✗ Snapshot: {ex.GetType().Name}");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Snapshot capture failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DownloadSnapshotsAsync for device {DeviceId}", deviceId);
            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }

            return new DownloadResult(
                Success: false,
                ErrorMessage: $"Download failed: {ex.Message}");
        }
    }

    /// <summary>A usable window is one where both bounds are set and start precedes end.</summary>
    public static bool HasRequestedWindow(DateTime startDate, DateTime endDate)
    {
        return startDate != default && endDate != default && startDate < endDate;
    }

    /// <summary>One snapshot to extract: the segment to download and the offset into it.</summary>
    public sealed record SnapshotTarget(RecordSegment Segment, TimeSpan Offset);

    /// <summary>
    /// Plans one frame per recorded segment overlapping the window, chronologically. The frame is taken
    /// at the segment start, or at the window start when the segment began earlier.
    /// </summary>
    public static IReadOnlyList<SnapshotTarget> PlanSnapshotTargets(
        IEnumerable<RecordSegment> segments, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        return segments
            .Where(s => s.End > windowStart && s.Begin < windowEnd)
            .OrderBy(s => s.Begin)
            .Select(s => new SnapshotTarget(s, s.Begin >= windowStart ? TimeSpan.Zero : windowStart - s.Begin))
            .ToList();
    }

    /// <summary>
    /// Lists recorded segments in the window, downloads each to a temp file, extracts a frame at the
    /// planned offset and discards the temp video.
    /// </summary>
    private async Task<DownloadResult> DownloadWindowedSnapshotsAsync(
        UniviewClient client, int channel, string deviceId, string outputPath,
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var windowStart = new DateTimeOffset(startDate.ToUniversalTime(), TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(endDate.ToUniversalTime(), TimeSpan.Zero);

        var segments = new List<RecordSegment>();
        foreach ((DateTimeOffset monthStart, DateTimeOffset monthEnd) in GetMonthChunks(startDate, endDate))
        {
            DateTimeOffset from = monthStart > windowStart ? monthStart : windowStart;
            DateTimeOffset to = monthEnd < windowEnd ? monthEnd : windowEnd;
            if (from >= to)
            {
                continue;
            }

            try
            {
                segments.AddRange(await client.ListSegmentsAsync(channel, from, to, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list segments for month {Month}, channel {Channel}", from.ToString("yyyy-MM"), channel);
                _activityLog.Enqueue($"✗ Failed to query {from:yyyy-MM}: {ex.Message}");
            }
        }

        IReadOnlyList<SnapshotTarget> targets = PlanSnapshotTargets(segments, windowStart, windowEnd);
        lock (_statusLock)
        {
            _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = targets.Count, FilesCompleted = 0, BytesDownloaded = 0 };
        }

        int filesDownloaded = 0;
        long bytesDownloaded = 0L;
        try
        {
            foreach (SnapshotTarget target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string jpg = EnsureWithinRoot(outputPath, Path.Combine(outputPath,
                    $"snapshot_{deviceId}_{target.Segment.Begin + target.Offset:yyyyMMddHHmmss}.jpg"));
                string tempVideo = EnsureWithinRoot(outputPath, Path.Combine(outputPath, $".snapshot_tmp_{Guid.NewGuid():N}.mp4"));

                try
                {
                    if (File.Exists(jpg) && new FileInfo(jpg).Length > 0)
                    {
                        filesDownloaded++;
                        bytesDownloaded += new FileInfo(jpg).Length;
                        continue;
                    }

                    await client.DownloadSegmentAsync(target.Segment, tempVideo, cancellationToken);
                    await client.CaptureFrameFromFileAsync(tempVideo, jpg, target.Offset, cancellationToken);

                    if (!File.Exists(jpg) || new FileInfo(jpg).Length == 0)
                    {
                        if (File.Exists(jpg))
                        {
                            File.Delete(jpg);
                        }

                        _activityLog.Enqueue($"✗ {Path.GetFileName(jpg)}: frame extraction produced no output");
                        continue;
                    }

                    long size = new FileInfo(jpg).Length;
                    filesDownloaded++;
                    bytesDownloaded += size;
                    lock (_statusLock)
                    {
                        _currentStatus = _currentStatus with { FilesCompleted = filesDownloaded, BytesDownloaded = bytesDownloaded, CurrentFile = jpg };
                    }

                    _activityLog.Enqueue($"✓ {Path.GetFileName(jpg)} ({FormatBytes(size)})");
                }
                catch (DownloadCapacityException ex)
                {
                    _logger.LogError(ex, "Device capacity limit hit while capturing snapshot; stopping batch");
                    lock (_statusLock)
                    {
                        _capacityBanUntilUtc = DateTime.UtcNow.AddMinutes(CapacityBanMinutes);
                    }

                    _activityLog.Enqueue($"✗ Capacity limit (code 60031): {ex.Message}");
                    return new DownloadResult(
                        Success: false,
                        FilesDownloaded: filesDownloaded,
                        BytesDownloaded: bytesDownloaded,
                        ErrorMessage: "Snapshots stopped: device capacity limit reached. " +
                                      "Wait for stale sessions to expire or reboot the NVR.",
                        FilesMatched: targets.Count);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture snapshot from segment {Begin:O} for channel {Channel}", target.Segment.Begin, channel);
                    _activityLog.Enqueue($"✗ {Path.GetFileName(jpg)}: {ex.GetType().Name}");
                }
                finally
                {
                    if (File.Exists(tempVideo))
                    {
                        File.Delete(tempVideo);
                    }
                }
            }
        }
        finally
        {
            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = false };
            }
        }

        _logger.LogDebug("Captured {Count} windowed snapshots ({Bytes} bytes) for device {DeviceId}", filesDownloaded, bytesDownloaded, deviceId);
        return new DownloadResult(
            Success: true,
            FilesDownloaded: filesDownloaded,
            BytesDownloaded: bytesDownloaded,
            ErrorMessage: targets.Count == 0 ? "No recorded footage found in the requested window" : null,
            FilesMatched: targets.Count);
    }

    public DownloadStatus GetStatus()
    {
        lock (_statusLock)
        {
            return _currentStatus;
        }
    }

    public DateTime? GetRateLimitBanUntilUtc()
    {
        lock (_statusLock)
        {
            return _capacityBanUntilUtc;
        }
    }

    public void OverrideRateLimitBan()
    {
        lock (_statusLock)
        {
            _capacityBanUntilUtc = null;
        }
    }

    public IReadOnlyList<string> DrainActivityLog()
    {
        var items = new List<string>();
        while (_activityLog.TryDequeue(out string? item))
        {
            items.Add(item);
        }

        return items;
    }

    /// <summary>Split a date range into month-sized chunks for querying.</summary>
    private static List<(DateTimeOffset Start, DateTimeOffset End)> GetMonthChunks(DateTime startDate, DateTime endDate)
    {
        var chunks = new List<(DateTimeOffset, DateTimeOffset)>();

        var current = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = endDate.ToUniversalTime();

        while (current <= end)
        {
            DateTime monthEnd = current.AddMonths(1).AddTicks(-1);
            if (monthEnd > end)
            {
                monthEnd = end;
            }

            chunks.Add((new DateTimeOffset(current), new DateTimeOffset(monthEnd)));
            current = current.AddMonths(1);
        }

        return chunks;
    }

    /// <summary>Try to parse a device id (channel number) from string format.</summary>
    private static bool TryParseChannelNumber(string deviceId, out int channel)
    {
        channel = 0;
        if (string.IsNullOrEmpty(deviceId))
        {
            return false;
        }

        // Support "channel:5", "5", "D5", etc.
        return deviceId.StartsWith("channel:", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(deviceId[8..], out channel)
            : deviceId.StartsWith("D", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(deviceId[1..], out channel)
            : int.TryParse(deviceId, out channel);
    }

    /// <summary>Build a destination file path for a segment using device id and segment timestamp.</summary>
    private static string BuildSegmentFileName(string outputPath, string deviceId, RecordSegment segment)
    {
        string timestamp = segment.Begin.ToString("yyyyMMddHHmmss");
        string fileName = $"ch{segment.Channel}_{timestamp}.mp4";
        return Path.Combine(outputPath, fileName);
    }

    /// <summary>Format bytes as human-readable (KB, MB).</summary>
    private static string FormatBytes(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        return bytes >= mb ? $"{bytes / mb:F1} MB" : bytes >= kb ? $"{bytes / kb:F1} KB" : $"{bytes} bytes";
    }

    /// <summary>Guard against path-traversal attacks by ensuring the resolved path stays within the intended root.</summary>
    private static string EnsureWithinRoot(string root, string candidatePath)
    {
        // Trim any trailing separator so callers passing e.g. Path.GetTempPath() (which
        // returns a trailing backslash on Windows) don't produce a spurious double separator
        // below and cause a legitimately-nested path to fail the StartsWith check.
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullCandidate = Path.GetFullPath(candidatePath);
        return !fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase)
            ? throw new InvalidOperationException($"Resolved path '{fullCandidate}' escapes the intended output directory '{fullRoot}'.")
            : fullCandidate;
    }
}
