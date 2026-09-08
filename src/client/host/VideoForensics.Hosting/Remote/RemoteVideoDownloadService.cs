using System.Net.Http.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
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
    /// background task. Status getter methods are synchronous and cached: they subscribe to
    /// ILiveHubConnection.DownloadProgressReceived in the constructor and return the latest received
    /// payload data (thread-safe via a simple lock), so polling GetProgress()/GetDownloadStatus()/etc.
    /// provides live updates without blocking HTTP calls.
    /// </summary>
    public class RemoteVideoDownloadService : IVideoDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly ILiveHubConnection _hubConnection;
        private DownloadProgressPayload? _lastPayload;
        private readonly object _lockObj = new object();
        private List<string> _activityLog = new();

        public RemoteVideoDownloadService(HttpClient httpClient, ILiveHubConnection hubConnection)
        {
            _httpClient = httpClient;
            _hubConnection = hubConnection;

            // Subscribe to hub progress updates and cache the latest payload for synchronous access.
            _hubConnection.DownloadProgressReceived += payload =>
            {
                lock (_lockObj)
                {
                    _lastPayload = payload;
                    // Accumulate activity messages so DrainActivityLog can return them all since last drain.
                    if (payload.Activity != null)
                    {
                        _activityLog.AddRange(payload.Activity);
                    }
                }
            };
        }

        /// <inheritdoc />
        public async Task<bool> DownloadVideosAsync(string outputPath, DateTime startDate, DateTime endDate, bool force = false)
        {
            var request = new DownloadVideosRequestDto(outputPath, startDate, endDate, force);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/downloads/videos", request);
            _ = response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DownloadOperationResponseDto>();
            return result?.Success ?? false;
        }

        /// <inheritdoc />
        public async Task<bool> DownloadSnapshotsAsync(string outputPath, DateTime startDate, DateTime endDate)
        {
            var request = new DownloadSnapshotsRequestDto(outputPath, startDate, endDate);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/downloads/snapshots", request);
            _ = response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<DownloadOperationResponseDto>();
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
            lock (_lockObj)
            {
                return _lastPayload?.PreScanCounts ?? new Dictionary<string, int>();
            }
        }

        /// <inheritdoc />
        public string GetDownloadStatus()
        {
            lock (_lockObj)
            {
                if (_lastPayload?.Progress.IsDownloading == true && _lastPayload.CurrentDeviceTotal > 0)
                {
                    return $"Downloading media for device {_lastPayload.CurrentDeviceIndex} of {_lastPayload.CurrentDeviceTotal}";
                }
                return _lastPayload?.Progress.IsDownloading == true ? "Downloading media..." : "Idle";
            }
        }

        /// <inheritdoc />
        public int GetRemainingCount()
        {
            lock (_lockObj)
            {
                if (_lastPayload?.Progress == null)
                    return 0;
                return Math.Max(0, _lastPayload.Progress.TotalFilesMatched - _lastPayload.Progress.TotalFilesCompleted);
            }
        }

        /// <inheritdoc />
        public string? GetRemainingReason()
        {
            // The cached payload doesn't currently carry a "remaining reason" field
            // (that's tracked server-side in the provider). Return null for now.
            return null;
        }

        /// <inheritdoc />
        public (int Index, int Total, string Name) GetCurrentDevice()
        {
            lock (_lockObj)
            {
                if (_lastPayload == null)
                    return (0, 0, "");
                return (_lastPayload.CurrentDeviceIndex, _lastPayload.CurrentDeviceTotal, _lastPayload.CurrentDeviceName ?? "");
            }
        }

        /// <inheritdoc />
        public DownloadStatus GetProgress()
        {
            lock (_lockObj)
            {
                return _lastPayload?.Progress ?? new DownloadStatus(false, 0, 0, 0);
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> DrainActivityLog()
        {
            lock (_lockObj)
            {
                var result = _activityLog;
                _activityLog = new List<string>();
                return result;
            }
        }

        /// <inheritdoc />
        public Task<bool> AuthenticateAsync(string username, string password)
        {
            throw new NotSupportedException("Not supported on a remote (MAUI client) service - use the server's API directly, or this operation isn't wired up yet.");
        }

        /// <inheritdoc />
        public string? GetLastError()
        {
            // Unlike other unsupported methods, return null instead of throwing - a UI calling this
            // defensively for display purposes shouldn't crash if nothing failed.
            return null;
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
