using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Providers.Common.Services
{
    /// <summary>
    /// Provider-neutral media pull. Splits the window into 30-day batches, pulls every device for each batch, and advances the
    /// watermark to a batch's end only when that batch pulled every matched event and actually found media. Once a batch leaves an event unpulled the
    /// watermark stays put for the rest of the run, so a later clean batch can never move it past that event; the next run
    /// starts from the watermark and re-reads the batch (already-downloaded media is skipped).
    /// </summary>
    /// <remarks>
    /// A 429 from the provider arrives as <see cref="DownloadResult.RateLimited"/>. The pull waits its <see cref="DownloadResult.RetryAfter"/>
    /// (or 60 seconds when none) and retries the same device batch, up to <c>maxDeviceAttempts</c> tries; it never aborts the run.
    /// </remarks>
    public sealed class EventMediaPuller : IEventMediaPuller
    {
        /// <summary>Length of one pull batch.</summary>
        public static readonly TimeSpan DefaultBatchLength = TimeSpan.FromDays(30);

        /// <summary>Minimum gap between provider media requests unless the caller sets another.</summary>
        public static readonly TimeSpan DefaultMinRequestInterval = TimeSpan.FromSeconds(1);

        /// <summary>Wait used after a 429 that carries no Retry-After.</summary>
        public static readonly TimeSpan DefaultThrottleWait = TimeSpan.FromSeconds(60);

        private readonly TimeSpan _batchLength;
        private readonly TimeSpan _minRequestInterval;
        private readonly Func<TimeSpan, CancellationToken, Task> _wait;
        private readonly int _maxDeviceAttempts;

        /// <summary>
        /// Creates the puller. Null arguments take the defaults. <paramref name="wait"/> is injectable so tests run without sleeping.
        /// </summary>
        public EventMediaPuller(
            TimeSpan? batchLength = null,
            TimeSpan? minRequestInterval = null,
            Func<TimeSpan, CancellationToken, Task>? wait = null,
            int maxDeviceAttempts = 3)
        {
            _batchLength = batchLength ?? DefaultBatchLength;
            _minRequestInterval = minRequestInterval ?? DefaultMinRequestInterval;
            _wait = wait ?? ((delay, ct) => Task.Delay(delay, ct));
            _maxDeviceAttempts = Math.Max(1, maxDeviceAttempts);
        }

        /// <inheritdoc />
        public async Task<MediaPullResult> PullAsync(IMediaDownloadService downloads, IMediaPullProgressStore progress, MediaPullRequest request, CancellationToken cancellationToken)
        {
            // One request in flight, spaced by the minimum interval, for every pull this run makes.
            downloads.SetRequestPacing(1, _minRequestInterval);

            DeviceTotals[] deviceTotals = request.Devices.Select(d => new DeviceTotals(d.DeviceId)).ToArray();
            int feedsFailed = 0;
            int batchesCompleted = 0;
            DateTime? watermark = null;
            bool watermarkFrozen = false;

            foreach ((DateTime batchStart, DateTime batchEnd) in MediaPullBatching.Split(request.WindowStartUtc, request.WindowEndUtc, _batchLength))
            {
                bool batchClean = true;
                bool batchHadEvents = false;
                for (int i = 0; i < request.Devices.Count; i++)
                {
                    DeviceBatchOutcome outcome = await PullDeviceBatchAsync(downloads, request.Devices[i], batchStart, batchEnd, cancellationToken);
                    deviceTotals[i].Add(outcome);
                    batchHadEvents |= outcome.Pulled + outcome.AlreadyPresent > 0;
                    if (outcome.FeedFailed)
                    {
                        feedsFailed++;
                    }

                    if (outcome.FeedFailed || outcome.NotPulled > 0)
                    {
                        batchClean = false;
                    }
                }

                batchesCompleted++;

                if (!request.AdvanceWatermark || watermarkFrozen)
                {
                    continue;
                }

                if (!batchClean)
                {
                    // Freeze for the rest of the run: a later clean batch must not move the watermark past this unpulled batch.
                    watermarkFrozen = true;
                }
                else if (batchHadEvents)
                {
                    // Advance only when this batch actually pulled or found media. A batch with no matched events moves nothing.
                    await progress.SetWatermarkUtcAsync(batchEnd, cancellationToken);
                    watermark = batchEnd;
                }
            }

            MediaPullDeviceResult[] devices = deviceTotals.Select(t => t.ToResult()).ToArray();
            return new MediaPullResult(
                Pulled: devices.Sum(d => d.Pulled),
                AlreadyPresent: devices.Sum(d => d.AlreadyPresent),
                NotPulled: devices.Sum(d => d.NotPulled),
                FeedsFailed: feedsFailed,
                Devices: devices,
                WatermarkUtc: watermark,
                BatchesCompleted: batchesCompleted);
        }

        /// <summary>Pulls one device for one batch, waiting out a rate limit and retrying the same batch.</summary>
        private async Task<DeviceBatchOutcome> PullDeviceBatchAsync(IMediaDownloadService downloads, MediaPullDevice device, DateTime batchStart, DateTime batchEnd, CancellationToken cancellationToken)
        {
            for (int attempt = 1; ; attempt++)
            {
                DownloadResult result = await downloads.DownloadVideosAsync(
                    device.DeviceId, device.OutputPath, batchStart, batchEnd, device.ProviderLocationId, cancellationToken);

                if (result.RateLimited && attempt < _maxDeviceAttempts)
                {
                    await _wait(result.RetryAfter ?? DefaultThrottleWait, cancellationToken);
                    continue;
                }

                if (!result.Success)
                {
                    // Nothing was pulled, so every matched event in the batch is not pulled.
                    int matched = await downloads.GetMatchedEventCountAsync(device.DeviceId, batchStart, batchEnd, cancellationToken);
                    return new DeviceBatchOutcome(Pulled: 0, AlreadyPresent: 0, NotPulled: matched, FeedFailed: true);
                }

                // FilesDownloaded includes files already on disk; those are reported separately and are not new pulls.
                int alreadyPresent = result.FilesAlreadyPresent;
                int pulled = Math.Max(0, result.FilesDownloaded - alreadyPresent);
                int notPulled = Math.Max(0, result.FilesMatched - result.FilesDownloaded);
                return new DeviceBatchOutcome(pulled, alreadyPresent, notPulled, FeedFailed: false);
            }
        }

        private sealed record DeviceBatchOutcome(int Pulled, int AlreadyPresent, int NotPulled, bool FeedFailed);

        /// <summary>Running totals for one device across every batch.</summary>
        private sealed class DeviceTotals
        {
            private readonly string _deviceId;
            private int _pulled;
            private int _alreadyPresent;
            private int _notPulled;

            public DeviceTotals(string deviceId)
            {
                _deviceId = deviceId;
            }

            public void Add(DeviceBatchOutcome outcome)
            {
                _pulled += outcome.Pulled;
                _alreadyPresent += outcome.AlreadyPresent;
                _notPulled += outcome.NotPulled;
            }

            public MediaPullDeviceResult ToResult()
            {
                return new(_deviceId, _pulled, _alreadyPresent, _notPulled);
            }
        }
    }
}
