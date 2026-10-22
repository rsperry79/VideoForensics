using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for SnapshotCapture records.</summary>
    public class SnapshotCaptureRepository : ISnapshotCaptureRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SnapshotCaptureRepository> _logger;

        public SnapshotCaptureRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<SnapshotCaptureRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Records one snapshot attempt (current or not).</summary>
        public async Task<SnapshotCapture> AddAsync(SnapshotCapture capture, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.SnapshotCaptures.Add(capture);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug(
                    "Snapshot capture recorded: {CaptureId} (device {DeviceId}, trigger: {Trigger}, status: {HttpStatus}, current: {IsCurrent})",
                    capture.Id, capture.DeviceId, capture.Trigger, capture.HttpStatus, capture.IsCurrent);
                return capture;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording snapshot capture for device {DeviceId}", capture.DeviceId);
                throw;
            }
        }

        /// <summary>Gets a device's current captures (IsCurrent == true) within a date range, oldest first.</summary>
        public async Task<IReadOnlyList<SnapshotCapture>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            // Normalize to UTC so the comparison matches the UTC values stored by SnapshotCapture.Create.
            DateTimeOffset fromUtc = from.ToUniversalTime();
            DateTimeOffset toUtc = to.ToUniversalTime();

            return await db.SnapshotCaptures
                .Where(c => c.DeviceId == deviceId && c.IsCurrent && c.CapturedAtUtc >= fromUtc && c.CapturedAtUtc <= toUtc)
                .OrderBy(c => c.CapturedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Gets every capture attempt (current or not) for a device within a date range, oldest first.</summary>
        public async Task<IReadOnlyList<SnapshotCapture>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            // Same UTC normalization as GetCurrentForDeviceAsync: stored values are UTC.
            DateTimeOffset fromUtc = from.ToUniversalTime();
            DateTimeOffset toUtc = to.ToUniversalTime();

            return await db.SnapshotCaptures
                .Where(c => c.DeviceId == deviceId && c.CapturedAtUtc >= fromUtc && c.CapturedAtUtc <= toUtc)
                .OrderBy(c => c.CapturedAtUtc)
                .ToListAsync(ct);
        }
    }
}
