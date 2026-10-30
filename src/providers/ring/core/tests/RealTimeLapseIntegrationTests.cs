using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

using FFMpegCore;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Core.Providers.Core;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Core.Tests.Mocks;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Live Ring check for the time lapse path. Takes six snapshots five seconds apart (30 seconds), records them the way
    /// the capture service does, then rolls them up into one loop. It writes real rows and JPEG files into the saved database
    /// and media folder, so it runs only when the database holds Ring credentials and is meant for manual runs.
    /// </summary>
    public class RealTimeLapseIntegrationTests : IAsyncLifetime
    {
        private const int SnapCount = 6;
        private static readonly TimeSpan SnapInterval = TimeSpan.FromSeconds(5);

        // A loop needs at least two frames to show any motion; fewer means Ring is not serving images, and the test should say so.
        private const int MinimumImages = 2;

        // Same metadata the capture service writes on a capture's Media row (SnapshotCaptureService.CaptureSnapshotMetadata).
        private const string CaptureSnapshotMetadata = "{\"type\":\"snapshot\",\"source\":\"capture\"}";

        public ValueTask InitializeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact]
        [Trait("Description", "Real API: six snapshots five seconds apart become one time lapse over the same start and end as the capture, and the snapshots are kept as evidence")]
        public async Task RealTimeLapse_SixSnapshotsFiveSecondsApart_RegistersLoopAndKeepsSnapshots()
        {
            RealSessionHelper.RequireLive();
            string ffprobe = ResolveFfprobeOrSkip();
            CancellationToken ct = TestContext.Current.CancellationToken;

            using IServiceScope scope = RealSessionHelper.Services.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            ProviderAccount? account = (await sp.GetRequiredService<IProviderAccountRepository>().ListActiveAsync(ct))
                .FirstOrDefault(a => string.Equals(a.ProviderName, "Ring", StringComparison.OrdinalIgnoreCase));
            if (account is null)
            {
                Assert.Skip("No active Ring account in the database.");
                return;
            }

            Session session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
            Entities.Devices ringDevices = await session.GetRingDevices();
            string wantedName = Environment.GetEnvironmentVariable("RING_TEST_DOORBOT") ?? "Front Door";
            Entities.Doorbot? doorbot = ringDevices.Doorbots.FirstOrDefault(d => string.Equals(d.Description, wantedName, StringComparison.OrdinalIgnoreCase));
            if (doorbot is null)
            {
                Assert.Skip($"No doorbot named '{wantedName}' on this account (set RING_TEST_DOORBOT).");
                return;
            }

            // The device must already be synced into the database, because the loop and its snapshot rows belong to that row.
            string providerDeviceId = doorbot.Id.ToString(CultureInfo.InvariantCulture);
            ILocationRepository locations = sp.GetRequiredService<ILocationRepository>();
            IDeviceRepository deviceRepository = sp.GetRequiredService<IDeviceRepository>();
            Device? device = null;
            Location? location = null;
            foreach (Location candidate in await locations.ListByProviderAccountIdAsync(account.Id, ct))
            {
                device = await deviceRepository.GetByProviderDeviceIdAsync(candidate.Id, providerDeviceId, ct);
                if (device is not null)
                {
                    location = candidate;
                    break;
                }
            }

            if (device is null || location is null)
            {
                Assert.Skip($"Doorbot '{wantedName}' is not synced into the database yet. Run a sync, then rerun.");
                return;
            }

            string mediaRoot = sp.GetRequiredService<IStorageLocationProvider>().GetEffectiveRoot(StorageCategory.Media, null);
            string folder = MediaFolderLayout.GetDeviceFolder(mediaRoot, account.ProviderName, location.Name, device.Name);
            _ = Directory.CreateDirectory(folder);

            IMediaItemRepository mediaItems = sp.GetRequiredService<IMediaItemRepository>();
            ISnapshotCaptureRepository captures = sp.GetRequiredService<ISnapshotCaptureRepository>();

            // Attempts are held to fixed slots, so the six are five seconds apart rather than five seconds after each one finishes.
            // The capture timeframe is fixed up front: it starts now and runs for six five-second slots (30 seconds).
            DateTimeOffset start = DateTimeOffset.UtcNow;
            DateTimeOffset end = start + (SnapInterval * SnapCount);
            var writtenFiles = new List<string>();
            int images = 0;
            for (int i = 0; i < SnapCount; i++)
            {
                TimeSpan wait = start + (SnapInterval * i) - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct);
                }

                DateTimeOffset attemptedAt = DateTimeOffset.UtcNow;
                await session.UpdateSnapshot(doorbot.Id);
                HttpUtility.DownloadResult result = await session.GetLatestSnapshotWithStatus(doorbot.Id, ct);

                // Ring builds the frame after the request, so the first read can still be 404. Retry only that status.
                for (int retry = 2; retry <= 4 && result.Status == HttpStatusCode.NotFound; retry++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * (retry - 1)), ct);
                    result = await session.GetLatestSnapshotWithStatus(doorbot.Id, ct);
                }

                string? mediaFilePath = null;
                if (result.Status == HttpStatusCode.OK && result.Bytes.Length > 0)
                {
                    mediaFilePath = Path.Combine(folder, attemptedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".jpg");
                    await File.WriteAllBytesAsync(mediaFilePath, result.Bytes, ct);
                    writtenFiles.Add(mediaFilePath);
                    images++;

                    await mediaItems.AddAsync(new MediaItem
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = device.Id,
                        FileName = Path.GetFileName(mediaFilePath),
                        FilePath = mediaFilePath,
                        MediaFormat = "jpg",
                        FileSizeBytes = result.Bytes.Length,
                        RecordedAtUtc = attemptedAt.UtcDateTime,
                        DownloadedAtUtc = DateTime.UtcNow,
                        Sha256Hash = Convert.ToHexString(SHA256.HashData(result.Bytes)).ToLowerInvariant(),
                        IntegrityVerified = false,
                        MetadataJson = CaptureSnapshotMetadata,
                    }, ct);
                }

                // Every attempt is recorded, including failures, so the roll-up can end a hold at a gap.
                _ = await captures.AddAsync(SnapshotCapture.Create(device.Id, attemptedAt, "fresh", (int)result.Status, mediaFilePath, deviceHealthId: null), ct);
            }

            Assert.True(images >= MinimumImages,
                $"Only {images} of {SnapCount} snapshots came back as images; at least {MinimumImages} are needed to build a loop.");

            // The roll-up is given the same start and end as the capture, so the loop must cover exactly that timeframe.
            ISnapshotRollupService rollup = sp.GetRequiredService<ISnapshotRollupService>();
            SnapshotRollupResult rollupResult = await rollup.RollUpAccountAsync(account.Id, start.UtcDateTime, end.UtcDateTime, ct);
            Assert.Null(rollupResult.ErrorMessage);

            // Every attempt in the timeframe is recorded inside [start, end), and the first one starts at the beginning of it.
            IReadOnlyList<SnapshotCapture> attempts = await captures.GetAttemptsForDeviceAsync(device.Id, start, end, ct);
            Assert.True(attempts.Count >= SnapCount, $"Expected at least {SnapCount} capture attempts in the timeframe, found {attempts.Count}.");
            Assert.All(attempts, a => Assert.InRange(a.CapturedAtUtc, start, end));
            Assert.True(attempts.Min(a => a.CapturedAtUtc) < start + SnapInterval, "The first capture did not start at the beginning of the timeframe.");

            // The loop is registered as a Media row on this device, marked with the roll-up marker and stamped with the window end.
            IReadOnlyList<MediaItem> inWindow = await mediaItems.GetByDeviceAndDateRangeAsync(device.Id, start.UtcDateTime.AddSeconds(-1), end.UtcDateTime.AddSeconds(1), ct);
            MediaItem? loop = inWindow
                .Where(m => m.MetadataJson == SnapshotRollupService.SnapshotRollupMarker)
                .OrderByDescending(m => m.RecordedAtUtc)
                .FirstOrDefault();
            Assert.NotNull(loop);
            Assert.Equal("mp4", loop.MediaFormat);
            Assert.True(File.Exists(loop.FilePath), $"The time lapse file is missing at {loop.FilePath}.");
            Assert.True(Math.Abs((loop.RecordedAtUtc - end.UtcDateTime).TotalSeconds) < 1, "The time lapse is not stamped with the end of the capture timeframe.");

            // The file itself must run for the capture timeframe, so the loop's length is read from the MP4 and compared to end - start.
            double lapseSeconds = await ProbeDurationSecondsAsync(ffprobe, loop.FilePath, ct);
            double captureSeconds = (end - start).TotalSeconds;
            Assert.True(Math.Abs(lapseSeconds - captureSeconds) <= 1,
                $"The time lapse runs {lapseSeconds:F1}s but the capture timeframe is {captureSeconds:F1}s.");

            // The snapshots are kept as evidence: the roll-up never deletes them. Hiding them in the timeline view is
            // TimeLapseCoverage.HideCovered in Ui.Shared, which this project does not reference, so TimeLapseCoverageTests covers that part.
            IReadOnlyList<MediaItem> snapshotRows = await mediaItems.GetSnapshotsByDeviceAndDateRangeAsync(device.Id, start.UtcDateTime.AddSeconds(-1), end.UtcDateTime.AddSeconds(1), ct);
            Assert.True(snapshotRows.Count >= images, $"Expected at least {images} snapshot rows to remain, found {snapshotRows.Count}.");
            foreach (string file in writtenFiles)
            {
                Assert.True(File.Exists(file), $"Snapshot file was removed: {file}");
            }
        }

        /// <summary>
        /// Resolves ffprobe the way the app does (config, bin folder, bundled, then PATH) and skips the test before the slow
        /// Ring capture when no executable is found.
        /// </summary>
        private static string ResolveFfprobeOrSkip()
        {
            string ffprobe = FfmpegPathResolver.Resolve(null, "ffprobe");
            if (!File.Exists(ffprobe))
            {
                Xunit.Assert.Skip("ffprobe was not found. Install ffmpeg or add its bin folder to PATH to run this check.");
            }

            return ffprobe;
        }

        /// <summary>Length of an MP4 in seconds, read with FFMpegCore so the test checks the file rather than the database row.</summary>
        private static async Task<double> ProbeDurationSecondsAsync(string ffprobe, string path, CancellationToken ct)
        {
            string? folder = Path.GetDirectoryName(ffprobe);
            var options = new FFOptions { BinaryFolder = string.IsNullOrEmpty(folder) ? null : folder };
            IMediaAnalysis analysis = await FFProbe.AnalyseAsync(path, options, ct);
            return analysis.Duration.TotalSeconds;
        }
    }
}
