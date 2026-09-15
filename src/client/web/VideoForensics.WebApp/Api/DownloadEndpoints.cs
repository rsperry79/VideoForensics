using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Video and snapshot download orchestration endpoints (plan §6).
    ///
    /// Per the plan's auth-default rule, EVERY endpoint here requires paired-device authentication
    /// (RequireAuthorization()) - there is no general exemption for read-only operations.
    ///
    /// FIRE-AND-FORGET DESIGN: DownloadVideosAsync, DownloadSnapshotsAsync, and PreScanAsync trigger
    /// long-running operations that run as background tasks on the server. The trigger endpoints return
    /// immediately with an acknowledgement, while the actual work proceeds independently. Progress is
    /// polled via GetProgress() and broadcast continuously over the SignalR hub (DownloadProgressBroadcastService)
    /// so remote clients (MAUI) can watch live updates without blocking HTTP requests. This prevents
    /// HTTP timeouts on lengthy downloads and allows multiple clients to observe the same in-progress
    /// operation simultaneously.
    /// </summary>
    public static class DownloadEndpoints
    {
        public static void MapDownloadEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/downloads").RequireAuthorization();

            // Trigger a video download as a background task.
            _ = group.MapPost("/videos", async (
                DownloadVideosRequestDto request,
                IVideoDownloadService downloadService,
                CancellationToken ct) =>
            {
                // Fire-and-forget: queue the download to run in the background while the HTTP response
                // returns immediately. The client polls GetProgress() and listens to the hub for updates.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = await downloadService.DownloadVideosAsync(request.OutputPath, request.StartDate, request.EndDate, request.Force);
                    }
                    catch
                    {
                        // Error is captured in GetLastError() on the service for client inspection
                    }
                }, ct);

                return Results.Accepted(null, new DownloadOperationResponseDto(
                    Success: true,
                    Message: "Video download queued as background task. Check /progress endpoint for live updates."
                ));
            })
            .RequireRateLimiting("media")
            .WithSummary("Trigger video download")
            .WithDescription("Queues a video download operation to run in the background across all paired devices. Returns immediately; monitor progress via /progress endpoint or the SignalR hub.");

            // Trigger a snapshot download as a background task.
            _ = group.MapPost("/snapshots", async (
                DownloadSnapshotsRequestDto request,
                IVideoDownloadService downloadService,
                CancellationToken ct) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = await downloadService.DownloadSnapshotsAsync(request.OutputPath, request.StartDate, request.EndDate);
                    }
                    catch
                    {
                        // Error is captured in GetLastError() on the service for client inspection
                    }
                }, ct);

                return Results.Accepted(null, new DownloadOperationResponseDto(
                    Success: true,
                    Message: "Snapshot download queued as background task. Check /progress endpoint for live updates."
                ));
            })
            .RequireRateLimiting("media")
            .WithSummary("Trigger snapshot download")
            .WithDescription("Queues a snapshot download operation to run in the background across all paired devices. Returns immediately; monitor progress via /progress endpoint or the SignalR hub.");

            // Trigger a pre-scan to count matched items across devices without downloading.
            _ = group.MapPost("/pre-scan", async (
                PreScanRequestDto request,
                IVideoDownloadService downloadService,
                CancellationToken ct) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await downloadService.PreScanAsync(request.OutputPath, request.StartDate, request.EndDate, request.Force, ct);
                    }
                    catch
                    {
                        // Error is captured in GetLastError() on the service for client inspection
                    }
                }, ct);

                return Results.Accepted(null, new DownloadOperationResponseDto(
                    Success: true,
                    Message: "Pre-scan queued as background task. Check /pre-scan-counts endpoint for live results."
                ));
            })
            .RequireRateLimiting("media")
            .WithSummary("Trigger pre-scan")
            .WithDescription("Queues a pre-scan operation to count matched items across devices without downloading. Results are accumulated in GetPreScanCounts() and broadcast via SignalR. Returns immediately.");

            // Get the current high-level download status message.
            _ = group.MapGet("/status", (IVideoDownloadService downloadService) =>
            {
                string status = downloadService.GetDownloadStatus();
                return Results.Ok(new DownloadStatusResponseDto(status));
            })
            .RequireRateLimiting("media")
            .WithSummary("Get download status")
            .WithDescription("Returns a human-readable status message describing the current download operation (e.g., 'Downloading media for device 2 of 5').");

            // Get live progress of the current download batch (files completed/total, bytes, current file).
            _ = group.MapGet("/progress", (IVideoDownloadService downloadService) =>
            {
                DownloadStatus progress = downloadService.GetProgress();
                var dto = new DownloadStatusDto(
                    IsDownloading: progress.IsDownloading,
                    FilesCompleted: progress.FilesCompleted,
                    FilesTotal: progress.FilesTotal,
                    BytesDownloaded: progress.BytesDownloaded,
                    CurrentFile: progress.CurrentFile,
                    TotalFilesCompleted: progress.TotalFilesCompleted,
                    TotalFilesMatched: progress.TotalFilesMatched,
                    TotalBytesDownloaded: progress.TotalBytesDownloaded,
                    ActiveConnections: progress.ActiveConnections,
                    CurrentSpeedMbps: progress.CurrentSpeedMbps
                );
                return Results.Ok(dto);
            })
            .RequireRateLimiting("media")
            .WithSummary("Get download progress")
            .WithDescription("Returns live progress metrics for the current download: files completed/total, bytes downloaded, current file name, and speed.");

            // Get per-device matched-item counts discovered during the most recent pre-scan or download.
            _ = group.MapGet("/pre-scan-counts", (IVideoDownloadService downloadService) =>
            {
                IReadOnlyDictionary<string, int> counts = downloadService.GetPreScanCounts();
                return Results.Ok(new PreScanCountsDto(counts));
            })
            .RequireRateLimiting("media")
            .WithSummary("Get pre-scan counts")
            .WithDescription("Returns per-device matched-item counts (items that matched the date range query), keyed by provider device ID. Updated live during an active pre-scan or download.");

            // Get remaining items and the reason they weren't downloaded.
            _ = group.MapGet("/remaining", (IVideoDownloadService downloadService) =>
            {
                int count = downloadService.GetRemainingCount();
                string? reason = downloadService.GetRemainingReason();
                return Results.Ok(new RemainingDto(count, reason));
            })
            .RequireRateLimiting("media")
            .WithSummary("Get remaining items")
            .WithDescription("Returns the count of matched items not downloaded (e.g., due to rate limit cutoff) and the reason why (e.g., 'Rate limited by Ring API'). Null reason does not guarantee nothing was skipped - the provider may not classify the reason.");

            // Get the current device being processed (1-based index, total count, device name).
            _ = group.MapGet("/current-device", (IVideoDownloadService downloadService) =>
            {
                (int index, int total, string name) = downloadService.GetCurrentDevice();
                return Results.Ok(new CurrentDeviceDto(index, total, name));
            })
            .RequireRateLimiting("media")
            .WithSummary("Get current device")
            .WithDescription("Returns information about which device is currently being processed: 1-based index, total device count, and device name/identifier. Helps explain why per-device counts reset during multi-device downloads.");
        }
    }
}
