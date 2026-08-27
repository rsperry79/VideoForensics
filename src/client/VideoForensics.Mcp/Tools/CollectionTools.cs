using System.ComponentModel;
using ModelContextProtocol.Server;
using VideoForensics.Client.Common;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for collecting (downloading) evidence from the Ring account.</summary>
    [McpServerToolType]
    public static class CollectionTools
    {
        [McpServerTool, Description("Downloads Ring videos recorded in [startDateUtc, endDateUtc] to outputPath. By default only fetches what's new since the last successful pull per device (watermark); pass force=true to re-scan the full window regardless. Requires prior authentication (see AccountTools.Authenticate).")]
        public static async Task<string> DownloadVideos(
            IVideoDownloadService downloadService,
            [Description("Directory to save downloaded video files to")] string outputPath,
            [Description("Start of the date range (UTC)")] DateTime startDateUtc,
            [Description("End of the date range (UTC)")] DateTime endDateUtc,
            [Description("If true, re-scans the full requested window instead of only fetching items newer than the last successful pull")] bool force = false)
        {
            var success = await downloadService.DownloadVideosAsync(outputPath, startDateUtc, endDateUtc, force);
            if (!success)
            {
                return $"Download failed: {downloadService.GetLastError() ?? "unknown error"}";
            }

            var progress = downloadService.GetProgress();
            var remaining = downloadService.GetRemainingCount();
            var summary = $"Download completed. {progress.TotalFilesCompleted} of {progress.TotalFilesMatched} matched file(s) downloaded, saved to {outputPath}.";
            if (remaining > 0)
            {
                summary += $" {remaining} more item(s) matched but were not downloaded (likely a rate limit paused the run) — call DownloadVideos again with the same parameters to continue.";
            }
            return summary;
        }

        [McpServerTool, Description("Captures the latest available snapshot from each Ring device and saves it to outputPath. Ring only exposes each device's current snapshot, not historical per-event snapshots, so startDateUtc/endDateUtc are accepted for API symmetry but don't affect which snapshot is captured. Requires prior authentication.")]
        public static async Task<string> DownloadSnapshots(
            IVideoDownloadService downloadService,
            [Description("Directory to save downloaded snapshot files to")] string outputPath,
            [Description("Start of the nominal date range (UTC); has no effect on which snapshot is captured")] DateTime startDateUtc,
            [Description("End of the nominal date range (UTC); has no effect on which snapshot is captured")] DateTime endDateUtc)
        {
            var success = await downloadService.DownloadSnapshotsAsync(outputPath, startDateUtc, endDateUtc);
            if (!success)
            {
                return $"Snapshot download failed: {downloadService.GetLastError() ?? "unknown error"}";
            }

            var progress = downloadService.GetProgress();
            return $"Snapshot download completed. {progress.TotalFilesCompleted} snapshot(s) captured, saved to {outputPath}.";
        }

        [McpServerTool, Description("Returns the current status of any in-flight or most recently completed download: files completed/matched, bytes downloaded, current speed, and the device currently being processed.")]
        public static object GetDownloadProgress(IVideoDownloadService downloadService)
        {
            var progress = downloadService.GetProgress();
            var (index, total, name) = downloadService.GetCurrentDevice();
            return new
            {
                progress.IsDownloading,
                progress.FilesCompleted,
                progress.FilesTotal,
                progress.BytesDownloaded,
                progress.CurrentFile,
                progress.TotalFilesCompleted,
                progress.TotalFilesMatched,
                progress.TotalBytesDownloaded,
                progress.ActiveConnections,
                progress.CurrentSpeedMbps,
                CurrentDeviceIndex = index,
                CurrentDeviceTotal = total,
                CurrentDeviceName = name,
                RemainingCount = downloadService.GetRemainingCount(),
                LastError = downloadService.GetLastError(),
                RecentActivity = downloadService.DrainActivityLog()
            };
        }
    }
}
