using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for DeviceHealth time-series entities.</summary>
    public class DeviceHealthRepository : IDeviceHealthRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceHealthRepository> _logger;

        public DeviceHealthRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceHealthRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Records a new device health metric.</summary>
        public async Task<DeviceHealth> AddAsync(DeviceHealth health, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.DeviceHealths.Add(health);
                _ = await db.SaveChangesAsync(ct);

                (string? DeviceName, string? LocationName) = await GetDeviceAndLocationNamesAsync(db, health.DeviceId, ct);
                _logger.LogInformation(
                    "Device health metric recorded: {HealthId} (device: {DeviceName} @ {LocationName} [{DeviceId}], online: {IsOnline})",
                    health.Id, DeviceName ?? "unknown", LocationName ?? "unknown", health.DeviceId, health.IsOnline);
                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording device health metric for device {DeviceId}", health.DeviceId);
                throw;
            }
        }

        /// <summary>
        /// Best-effort lookup of a device's name and its location's name, purely for human-readable
        /// log output - a raw device GUID in a log line isn't actionable for an operator scanning
        /// logs, but "Front Door @ 123 Main St" is. Never lets a lookup failure break the health
        /// metric that was already successfully persisted above.
        /// </summary>
        private async Task<(string? DeviceName, string? LocationName)> GetDeviceAndLocationNamesAsync(VideoForensicsDbContext db, Guid deviceId, CancellationToken ct)
        {
            try
            {
                var device = await db.Devices
                    .Where(d => d.Id == deviceId)
                    .Select(d => new { d.Name, d.LocationId })
                    .FirstOrDefaultAsync(ct);

                if (device is null)
                {
                    return (null, null);
                }

                string? locationName = await db.Locations
                    .Where(l => l.Id == device.LocationId)
                    .Select(l => l.Name)
                    .FirstOrDefaultAsync(ct);

                return (device.Name, locationName);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve device/location name for log output (device {DeviceId})", deviceId);
                return (null, null);
            }
        }

        /// <summary>Gets the latest health metric for a device.</summary>
        public async Task<DeviceHealth?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealths
                .Where(dh => dh.DeviceId == deviceId)
                .OrderByDescending(dh => dh.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Gets the full health history for a device, newest first.</summary>
        public async Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealths
                .Where(dh => dh.DeviceId == deviceId)
                .OrderByDescending(dh => dh.CapturedAtUtc)
                .ToListAsync(ct);
        }
    }
}
