using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Common.Services;

using Xunit;

namespace VideoForensics.Core.Providers.Common.Tests.Services
{
    /// <summary>Shared pull logic: 30-day batching, the watermark advance rule, resume, pacing and 429 wait-and-retry.</summary>
    public class EventMediaPullerTests
    {
        private static readonly DateTime End = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime OneYearAgo = End.AddDays(-365);
        private static readonly TimeSpan Batch = TimeSpan.FromDays(30);
        private static readonly MediaPullDevice Device = new("device-1", "out", "location-1");

        /// <summary>In-memory media source. Responses are scripted per call; every call is recorded.</summary>
        private sealed class FakeDownloads : IMediaDownloadService
        {
            public readonly List<(string Device, DateTime Start, DateTime End)> Calls = [];
            public readonly List<(int Max, TimeSpan Interval)> Pacing = [];
            public int MatchedCount;
            public Func<int, DateTime, DownloadResult> Respond = (_, _) => new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1);

            public Task<DownloadResult> DownloadVideosAsync(string deviceId, string outputPath, DateTime startDate, DateTime endDate, string? providerLocationId = null, CancellationToken cancellationToken = default)
            {
                Calls.Add((deviceId, startDate, endDate));
                int attempt = Calls.Count(c => c.Start == startDate);
                return Task.FromResult(Respond(attempt, startDate));
            }

            public Task<DownloadResult> DownloadSnapshotsAsync(string deviceId, string outputPath, DateTime startDate, DateTime endDate, string? providerLocationId = null, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public void SetRequestPacing(int maxConcurrent, TimeSpan minRequestInterval)
            {
                Pacing.Add((maxConcurrent, minRequestInterval));
            }

            public DownloadStatus GetStatus()
            {
                return new(false, 0, 0, 0);
            }

            public Task<int> GetMatchedEventCountAsync(string deviceId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(MatchedCount);
            }
        }

        /// <summary>In-memory watermark store that records every value written, in order.</summary>
        private sealed class FakeProgress : IMediaPullProgressStore
        {
            public DateTime? Watermark;
            public readonly List<DateTime> Saved = [];

            public Task<DateTime?> GetWatermarkUtcAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Watermark);
            }

            public Task SetWatermarkUtcAsync(DateTime watermarkUtc, CancellationToken cancellationToken)
            {
                Watermark = watermarkUtc;
                Saved.Add(watermarkUtc);
                return Task.CompletedTask;
            }
        }

        private static MediaPullRequest Request(DateTime start, DateTime end, bool advance = true)
        {
            return new(start, end, advance, new[] { Device });
        }

        private static EventMediaPuller CreatePuller(List<TimeSpan>? waits = null)
        {
            return new(
            batchLength: Batch,
            minRequestInterval: TimeSpan.FromSeconds(1),
            wait: (wait, _) => { waits?.Add(wait); return Task.CompletedTask; });
        }

        [Fact]
        public async Task PullAsync_OneYearWindow_CreatesThirteenBatchesInThirtyDaySteps()
        {
            var downloads = new FakeDownloads();

            _ = await CreatePuller().PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, End), CancellationToken.None);

            Assert.Equal(13, downloads.Calls.Count);
            Assert.Equal(OneYearAgo, downloads.Calls[0].Start);
            for (int i = 1; i < 12; i++)
            {
                Assert.Equal(downloads.Calls[i - 1].End, downloads.Calls[i].Start);
                Assert.Equal(Batch, downloads.Calls[i].End - downloads.Calls[i].Start);
            }

            Assert.Equal(End, downloads.Calls[^1].End);
            Assert.Equal(TimeSpan.FromDays(5), downloads.Calls[^1].End - downloads.Calls[^1].Start);
        }

        [Fact]
        public async Task PullAsync_CleanBatches_AdvanceWatermarkToEachBatchEnd()
        {
            var progress = new FakeProgress();

            MediaPullResult result = await CreatePuller().PullAsync(new FakeDownloads(), progress, Request(OneYearAgo, End), CancellationToken.None);

            Assert.Equal(13, progress.Saved.Count);
            Assert.Equal(OneYearAgo + Batch, progress.Saved[0]);
            Assert.Equal(End, progress.Saved[^1]);
            Assert.Equal(End, result.WatermarkUtc);
        }

        [Fact]
        public async Task PullAsync_BatchWithNotPulledEvent_DoesNotAdvanceWatermarkPastIt()
        {
            var downloads = new FakeDownloads
            {
                // Second batch matched one event that was not pulled; later batches pull cleanly.
                Respond = (attempt, start) => start == OneYearAgo + Batch
                    ? new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 1)
                    : new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1),
            };
            var progress = new FakeProgress();

            MediaPullResult result = await CreatePuller().PullAsync(downloads, progress, Request(OneYearAgo, End), CancellationToken.None);

            // Only the batch before the unpulled event moved the watermark; later clean batches must not pass it.
            Assert.Equal(new[] { OneYearAgo + Batch }, progress.Saved);
            Assert.Equal(OneYearAgo + Batch, result.WatermarkUtc);
            Assert.Equal(1, result.NotPulled);
        }

        [Fact]
        public async Task PullAsync_InterruptedMidBatch_ResumesFromLastCompletedBatch()
        {
            var progress = new FakeProgress();
            var interrupted = new FakeDownloads
            {
                Respond = (_, start) => start == OneYearAgo + Batch
                    ? throw new OperationCanceledException()
                    : new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1),
            };

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                CreatePuller().PullAsync(interrupted, progress, Request(OneYearAgo, End), CancellationToken.None));

            // The first batch completed and was recorded; the second was interrupted and must be retried.
            Assert.Equal(OneYearAgo + Batch, progress.Watermark);

            var resumed = new FakeDownloads();
            DateTime resumeFrom = await progress.GetWatermarkUtcAsync(CancellationToken.None) ?? OneYearAgo;
            _ = await CreatePuller().PullAsync(resumed, progress, Request(resumeFrom, End), CancellationToken.None);

            Assert.Equal(OneYearAgo + Batch, resumed.Calls[0].Start);
        }

        [Fact]
        public async Task PullAsync_AnyRun_SetsOneInFlightAndMinimumIntervalOnDownloads()
        {
            var downloads = new FakeDownloads();

            _ = await CreatePuller().PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, End), CancellationToken.None);

            Assert.Equal(new[] { (1, TimeSpan.FromSeconds(1)) }, downloads.Pacing);
        }

        [Fact]
        public async Task PullAsync_RateLimitedWithRetryAfter_WaitsRetryAfterThenRetriesSameBatch()
        {
            var waits = new List<TimeSpan>();
            var downloads = new FakeDownloads
            {
                Respond = (attempt, _) => attempt == 1
                    ? new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 1, RateLimited: true, RetryAfter: TimeSpan.FromSeconds(7))
                    : new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1),
            };

            MediaPullResult result = await CreatePuller(waits).PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, OneYearAgo + TimeSpan.FromDays(10)), CancellationToken.None);

            Assert.Equal(new[] { TimeSpan.FromSeconds(7) }, waits);
            Assert.Equal(2, downloads.Calls.Count);
            Assert.Equal(downloads.Calls[0].Start, downloads.Calls[1].Start);
            Assert.Equal(1, result.Pulled);
            Assert.Equal(0, result.NotPulled);
        }

        [Fact]
        public async Task PullAsync_RateLimitedWithoutRetryAfter_Waits60Seconds()
        {
            var waits = new List<TimeSpan>();
            var downloads = new FakeDownloads
            {
                Respond = (attempt, _) => attempt == 1
                    ? new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 1, RateLimited: true)
                    : new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1),
            };

            _ = await CreatePuller(waits).PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, OneYearAgo + TimeSpan.FromDays(10)), CancellationToken.None);

            Assert.Equal(new[] { TimeSpan.FromSeconds(60) }, waits);
        }

        [Fact]
        public async Task PullAsync_AlreadyPresentFiles_CountedAsPulledButNotNew()
        {
            var downloads = new FakeDownloads
            {
                Respond = (_, _) => new DownloadResult(Success: true, FilesDownloaded: 2, FilesMatched: 2, FilesAlreadyPresent: 2),
            };

            MediaPullResult result = await CreatePuller().PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, OneYearAgo + TimeSpan.FromDays(10)), CancellationToken.None);

            Assert.Equal(0, result.Pulled);
            Assert.Equal(2, result.AlreadyPresent);
            Assert.Equal(0, result.Devices.Single().Pulled);
        }

        [Fact]
        public async Task PullAsync_DownloadFails_CountsFeedFailureAndMatchedEventsAsNotPulled()
        {
            var downloads = new FakeDownloads
            {
                MatchedCount = 3,
                Respond = (_, _) => new DownloadResult(Success: false, ErrorMessage: "not authenticated"),
            };

            MediaPullResult result = await CreatePuller().PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, OneYearAgo + TimeSpan.FromDays(10)), CancellationToken.None);

            Assert.Equal(1, result.FeedsFailed);
            Assert.Equal(3, result.NotPulled);
            Assert.Equal(0, result.Pulled);
        }

        [Fact]
        public async Task PullAsync_NewEventsPulled_ReportsDeviceAsPulled()
        {
            var downloads = new FakeDownloads
            {
                Respond = (_, _) => new DownloadResult(Success: true, FilesDownloaded: 2, FilesMatched: 2),
            };

            MediaPullResult result = await CreatePuller().PullAsync(downloads, new FakeProgress(), Request(OneYearAgo, OneYearAgo + TimeSpan.FromDays(10)), CancellationToken.None);

            Assert.Equal(2, result.Pulled);
            Assert.Equal(2, result.Devices.Single().Pulled);
        }

        [Fact]
        public async Task PullAsync_NormalSyncWithWatermark_StartsFromGivenWatermark()
        {
            DateTime watermark = End.AddDays(-3);
            var downloads = new FakeDownloads();

            _ = await CreatePuller().PullAsync(downloads, new FakeProgress { Watermark = watermark }, Request(watermark, End), CancellationToken.None);

            _ = Assert.Single(downloads.Calls);
            Assert.Equal(watermark, downloads.Calls[0].Start);
        }

        [Fact]
        public void MediaPullBatching_Split_ProducesThirtyDayWindowsCoveringTheRange()
        {
            var batches = MediaPullBatching.Split(OneYearAgo, End, Batch).ToList();

            Assert.Equal(13, batches.Count);
            Assert.Equal(End, batches[^1].End);
        }
    }
}
