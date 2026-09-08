using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Uniview.Services;

/// <summary>
/// Uniview media download service. Handles video and snapshot downloads from Uniview NVR devices.
/// Implements the month-chunking pattern required by the API (see docs/NVR_API.md section 9).
/// Thread-safe for concurrent status reads and activity log drains while downloads happen.
/// </summary>
public class UniviewMediaDownloadService : IMediaDownloadService
{
    private readonly ILogger<UniviewMediaDownloadService> _logger;
    private readonly IUniviewSessionProvider _sessionProvider;

    // Mutable state tracking — accessed from download thread and status-query threads
    private DownloadStatus _currentStatus = new(IsDownloading: false, FilesCompleted: 0, FilesTotal: 0, BytesDownloaded: 0);
    private readonly object _statusLock = new();

    // Per-item outcomes queued for draining by UI
    private readonly ConcurrentQueue<string> _activityLog = new();

    // Capacity exception ban tracking: device-side concurrency limit (code 60031)
    // Not time-based like cloud rate limits, but treat as "banned for N minutes"
    private DateTime? _capacityBanUntilUtc;
    private const int CapacityBanMinutes = 5;

    public UniviewMediaDownloadService(
        ILogger<UniviewMediaDownloadService> logger,
        IUniviewSessionProvider sessionProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionProvider = sessionProvider ?? throw new ArgumentNullException(nameof(sessionProvider));
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
            _logger.LogInformation("Downloading videos for device {DeviceId} from {StartDate} to {EndDate}",
                deviceId, startDate, endDate);

            var client = _sessionProvider.GetClient();
            if (client is null)
            {
                _logger.LogError("Not authenticated: Client is null");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number (expect numeric or "channel:<num>" format)
            if (!TryParseChannelNumber(deviceId, out var channel))
            {
                _logger.LogError("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            _ = Directory.CreateDirectory(outputPath);

            // Split date range into month-sized chunks and query each
            var allSegments = new List<RecordSegment>();
            var monthChunks = GetMonthChunks(startDate, endDate);

            foreach (var (monthStart, monthEnd) in monthChunks)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var monthSegments = await client.ListSegmentsAsync(channel, monthStart, monthEnd, cancellationToken);
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
                    _logger.LogWarning(ex, "Failed to list segments for month {Month}, channel {Channel}",
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

            _logger.LogInformation("Found {SegmentCount} segments across all months for channel {Channel}",
                allSegments.Count, channel);

            var filesDownloaded = 0;
            var bytesDownloaded = 0L;

            // Download each segment sequentially
            foreach (var segment in allSegments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var fileName = BuildSegmentFileName(outputPath, deviceId, segment);

                try
                {
                    // Skip if already exists
                    if (File.Exists(fileName) && new FileInfo(fileName).Length > 0)
                    {
                        var existingSize = new FileInfo(fileName).Length;
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

                    var fileSize = new FileInfo(fileName).Length;
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
                    _logger.LogWarning(ex, "Failed to download segment {Begin:O} for channel {Channel}",
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
    /// Uniview devices have no device-side historical snapshot capability — only live RTSP frame grab.
    /// Captures a current-moment JPEG via ffmpeg and clearly documents that only a live snapshot was
    /// captured, not historical ones for the requested range.
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
            _logger.LogInformation("Requesting live snapshot for device {DeviceId}", deviceId);

            var client = _sessionProvider.GetClient();
            if (client is null)
            {
                _logger.LogError("Not authenticated: Client is null");
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: "Download failed: not authenticated");
            }

            // Parse device id to channel number
            if (!TryParseChannelNumber(deviceId, out var channel))
            {
                _logger.LogError("Invalid device id: {DeviceId}", deviceId);
                return new DownloadResult(
                    Success: false,
                    ErrorMessage: $"Invalid device id: {deviceId}");
            }

            _ = Directory.CreateDirectory(outputPath);

            lock (_statusLock)
            {
                _currentStatus = _currentStatus with { IsDownloading = true, FilesTotal = 1, FilesCompleted = 0 };
            }

            var fileName = Path.Combine(outputPath, $"snapshot_{deviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg");

            try
            {
                await client.CaptureLiveSnapshotAsync(channel, fileName, cancellationToken);

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

                var fileSize = new FileInfo(fileName).Length;
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

                var msg = $"✓ {Path.GetFileName(fileName)} ({FormatBytes(fileSize)}) — " +
                          "Live snapshot only (Uniview device has no historical snapshot API)";
                _activityLog.Enqueue(msg);

                _logger.LogInformation("Downloaded live snapshot ({Bytes} bytes) for device {DeviceId}",
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
        while (_activityLog.TryDequeue(out var item))
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
        var end = endDate.ToUniversalTime();

        while (current <= end)
        {
            var monthEnd = current.AddMonths(1).AddTicks(-1);
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
        if (deviceId.StartsWith("channel:", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(deviceId.Substring(8), out channel);
        }

        if (deviceId.StartsWith("D", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(deviceId.Substring(1), out channel);
        }

        return int.TryParse(deviceId, out channel);
    }

    /// <summary>Build a destination file path for a segment using device id and segment timestamp.</summary>
    private static string BuildSegmentFileName(string outputPath, string deviceId, RecordSegment segment)
    {
        var timestamp = segment.Begin.ToString("yyyyMMddHHmmss");
        var fileName = $"ch{segment.Channel}_{timestamp}.mp4";
        return Path.Combine(outputPath, fileName);
    }

    /// <summary>Format bytes as human-readable (KB, MB).</summary>
    private static string FormatBytes(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        if (bytes >= mb)
        {
            return $"{bytes / mb:F1} MB";
        }
        return bytes >= kb ? $"{bytes / kb:F1} KB" : $"{bytes} bytes";
    }
}
