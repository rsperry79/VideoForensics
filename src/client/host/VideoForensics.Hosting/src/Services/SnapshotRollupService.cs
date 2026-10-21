using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Hosting.Contracts;
using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Rolls an account's snapshots up into one MP4 loop per device. The timeline builder does the encoding from the stored
    /// capture rows, so the roll-up adds no second encoder. Each loop is registered as a Media row; the JPEG captures
    /// are left in place as evidence.
    /// </summary>
    public sealed class SnapshotRollupService : ISnapshotRollupService
    {
        private readonly ILocationRepository _locations;
        private readonly IDeviceRepository _devices;
        private readonly ISnapshotTimelineService _timeline;
        private readonly IMediaItemRepository _mediaItems;
        private readonly IOptions<SnapshotTimelineOptions> _options;
        private readonly ILogger<SnapshotRollupService> _logger;

        public SnapshotRollupService(
            ILocationRepository locations,
            IDeviceRepository devices,
            ISnapshotTimelineService timeline,
            IMediaItemRepository mediaItems,
            IOptions<SnapshotTimelineOptions> options,
            ILogger<SnapshotRollupService> logger)
        {
            _locations = locations;
            _devices = devices;
            _timeline = timeline;
            _mediaItems = mediaItems;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SnapshotRollupResult> RollUpAccountAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            var devices = new List<DeviceEntity>();
            foreach (Location location in await _locations.ListByProviderAccountIdAsync(accountId, cancellationToken))
            {
                devices.AddRange(await _devices.GetByLocationIdAsync(location.Id, cancellationToken));
            }

            int rolledUp = 0;
            int failed = 0;
            string? firstError = null;

            foreach (DeviceEntity device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // The loop file is named by its window end, so a retried window gets a fresh path.
                    string outputPath = Path.Combine(_options.Value.SnapshotRoot, "rollups", device.Id.ToString("D"), $"{toUtc:yyyyMMddHHmmss}.mp4");
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                    string? built = await _timeline.BuildDeviceTimelineAsync(
                        device.Id,
                        new DateTimeOffset(fromUtc, TimeSpan.Zero),
                        new DateTimeOffset(toUtc, TimeSpan.Zero),
                        outputPath,
                        cancellationToken);

                    // No stills in the window is not a failure: there was nothing to roll up.
                    if (built is null)
                    {
                        continue;
                    }

                    await RecordLoopAsync(device, built, toUtc, cancellationToken);
                    rolledUp++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Snapshot roll-up failed for {DeviceName}", device.Name);
                    firstError ??= $"{device.Name}: {ex.Message}";
                }
            }

            string? error = failed > 0 ? $"{failed} of {devices.Count} device(s) failed the roll-up: {firstError}" : null;
            return new SnapshotRollupResult(devices.Count, rolledUp, error);
        }

        /// <summary>Registers the MP4 as a Media row. RecordedAtUtc is the window end, which is unique per roll-up tick.</summary>
        private async Task RecordLoopAsync(DeviceEntity device, string mp4Path, DateTime windowEndUtc, CancellationToken cancellationToken)
        {
            byte[] hash;
            await using (FileStream stream = File.OpenRead(mp4Path))
            {
                hash = await SHA256.HashDataAsync(stream, cancellationToken);
            }

            await _mediaItems.AddAsync(new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                FileName = Path.GetFileName(mp4Path),
                FilePath = mp4Path,
                MediaFormat = "mp4",
                FileSizeBytes = new FileInfo(mp4Path).Length,
                RecordedAtUtc = windowEndUtc,
                DownloadedAtUtc = DateTime.UtcNow,
                Sha256Hash = Convert.ToHexString(hash).ToLowerInvariant(),
                IntegrityVerified = false,
            }, cancellationToken);
        }
    }
}
