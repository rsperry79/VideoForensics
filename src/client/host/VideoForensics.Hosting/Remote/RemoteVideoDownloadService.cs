using System.Net.Http.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IVideoDownloadService"/> that calls the server's download orchestration
    /// endpoints (VideoForensics.WebApp/Api/DownloadEndpoints.cs) instead of a local provider.
    /// Implements the "remote" half of the client/server split for download operations (plan §6).
    ///
    /// Trigger methods (DownloadVideosAsync, DownloadSnapshotsAsync, PreScanAsync) POST to the server
    /// and return immediately after the 202 Accepted response; the server runs the actual work as a
    /// background task. Status getter methods are synchronous and read the singleton <see cref="IRealtimeStore"/>,
    /// which holds the latest DownloadProgress payload. This service holds no hub subscription of its own. It is
    /// a transient typed client, so a per-instance subscription would leak onto the singleton hub.
    /// </summary>
    public class RemoteVideoDownloadService : IVideoDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly IRealtimeStore _store;

        /// <summary>Creates the service over the shared store. The store is the only hub subscriber.</summary>
        public RemoteVideoDownloadService(HttpClient httpClient, IRealtimeStore store)
        {
            _httpClient = httpClient;
            _store = store;
        }

        /// <inheritdoc />
        public async Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false)
        {
            var request = new DownloadVideosRequestDto(outputPath, startDate, endDate, force);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/downloads/videos", request);
            _ = response.EnsureSuccessStatusCode();
            DownloadOperationResponseDto? result = await response.Content.ReadFromJsonAsync<DownloadOperationResponseDto>();
            return result?.Success ?? false;
        }

        /// <inheritdoc />
        public async Task<bool> DownloadSnapshotsAsync(string outputPath, DateTime startDate, DateTime endDate)
        {
            var request = new DownloadSnapshotsRequestDto(outputPath, startDate, endDate);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/downloads/snapshots", request);
            _ = response.EnsureSuccessStatusCode();
            DownloadOperationResponseDto? result = await response.Content.ReadFromJsonAsync<DownloadOperationResponseDto>();
            return result?.Success ?? false;
        }

        /// <inheritdoc />
        public async Task PreScanAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false, CancellationToken cancellationToken = default)
        {
            var request = new PreScanRequestDto(outputPath, startDate, endDate, force);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/downloads/pre-scan", request, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, int> GetPreScanCounts()
        {
            return _store.LatestDownloadProgress?.PreScanCounts ?? new Dictionary<string, int>();
        }

        /// <inheritdoc />
        public string GetDownloadStatus()
        {
            DownloadProgressDto? last = _store.LatestDownloadProgress;
            return last?.Progress.IsDownloading == true && last.CurrentDeviceTotal > 0
                ? $"Downloading media for device {last.CurrentDeviceIndex} of {last.CurrentDeviceTotal}"
                : last?.Progress.IsDownloading == true ? "Downloading media..." : "Idle";
        }

        /// <inheritdoc />
        public int GetRemainingCount()
        {
            DownloadProgressDto? last = _store.LatestDownloadProgress;
            return last?.Progress == null
                ? 0
                : Math.Max(0, last.Progress.TotalFilesMatched - last.Progress.TotalFilesCompleted);
        }

        /// <inheritdoc />
        public string? GetRemainingReason()
        {
            return _store.LatestDownloadProgress?.RemainingReason;
        }

        /// <inheritdoc />
        public (int Index, int Total, string Name) GetCurrentDevice()
        {
            DownloadProgressDto? last = _store.LatestDownloadProgress;
            if (last == null)
            {
                return (0, 0, "");
            }

            return (last.CurrentDeviceIndex, last.CurrentDeviceTotal, last.CurrentDeviceName ?? "");
        }

        /// <inheritdoc />
        public DownloadStatus GetProgress()
        {
            return _store.LatestDownloadProgress?.Progress.ToDomain() ?? new DownloadStatus(false, 0, 0, 0);
        }

        /// <inheritdoc />
        public IReadOnlyList<string> DrainActivityLog()
        {
            return _store.DrainActivityLog();
        }

        /// <inheritdoc />
        public Task<bool> AuthenticateAsync(string username, string password)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) service - use the server's API directly, or this operation isn't wired up yet.");
        }

        /// <inheritdoc />
        public string? GetLastError()
        {
            // Returns null (not a throw) when no progress has been received or nothing failed, so a UI
            // calling this defensively for display purposes doesn't crash.
            return _store.LatestDownloadProgress?.LastError;
        }

        /// <inheritdoc />
        public DateTime? GetRateLimitBanUntilUtc()
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) service - use the server's API directly, or this operation isn't wired up yet.");
        }

        /// <inheritdoc />
        public void OverrideRateLimitBan()
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) service - use the server's API directly, or this operation isn't wired up yet.");
        }
    }
}
