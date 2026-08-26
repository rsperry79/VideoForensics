using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for append-only DeviceConfigSnapshot entities.</summary>
    public class DeviceConfigRepository : IDeviceConfigRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceConfigRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceConfigRepository.</summary>
        public DeviceConfigRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceConfigRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a device config snapshot by ID.</summary>
        public async Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceConfigSnapshots.FirstOrDefaultAsync(dcs => dcs.Id == snapshotId, ct);
        }

        /// <summary>Appends a new device configuration snapshot.</summary>
        public async Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceConfigSnapshots.Add(snapshot);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device config snapshot appended: {SnapshotId} (device: {DeviceId})",
                    snapshot.Id, snapshot.DeviceId);
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending device config snapshot for device {DeviceId}", snapshot.DeviceId);
                throw;
            }
        }

        /// <summary>Gets the latest device configuration snapshot for a device.</summary>
        public async Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceConfigSnapshots
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Gets the full history of device configuration snapshots for a device.</summary>
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceConfigSnapshots
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .ToListAsync(ct);
        }

        /// <summary>Lists all device config snapshots.</summary>
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceConfigSnapshots.ToListAsync(ct);
        }
    }
}
