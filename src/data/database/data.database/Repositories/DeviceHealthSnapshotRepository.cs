using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for append-only DeviceHealthSnapshot entities.</summary>
    public class DeviceHealthSnapshotRepository : IDeviceHealthSnapshotRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceHealthSnapshotRepository> _logger;

        public DeviceHealthSnapshotRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceHealthSnapshotRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Appends a new device health snapshot.</summary>
        public async Task<DeviceHealthSnapshot> AppendSnapshotAsync(DeviceHealthSnapshot snapshot, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceHealthSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device health snapshot appended: {SnapshotId} (device: {DeviceId})",
                    snapshot.Id, snapshot.DeviceId);
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending device health snapshot for device {DeviceId}", snapshot.DeviceId);
                throw;
            }
        }

        /// <summary>Gets the nearest snapshot at or before the given time - the "last known state" going into a gap.</summary>
        public async Task<DeviceHealthSnapshot?> GetLatestBeforeAsync(Guid deviceId, DateTime atOrBeforeUtc, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealthSnapshots
                .Where(s => s.DeviceId == deviceId && s.CapturedAtUtc <= atOrBeforeUtc)
                .OrderByDescending(s => s.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Gets the full snapshot history for a device, newest first.</summary>
        public async Task<IReadOnlyList<DeviceHealthSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealthSnapshots
                .Where(s => s.DeviceId == deviceId)
                .OrderByDescending(s => s.CapturedAtUtc)
                .ToListAsync(ct);
        }
    }
}
