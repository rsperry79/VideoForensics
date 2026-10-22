using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Reads a device's typed settings rows (offline motion, detection types, thresholds).</summary>
    public class DeviceTypedSettingsReader : IDeviceTypedSettingsReader
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public DeviceTypedSettingsReader(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public async Task<DeviceTypedSettings> GetAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            DeviceOfflineMotionSettings? offline = await db.DeviceOfflineMotionSettings.AsNoTracking().FirstOrDefaultAsync(s => s.DeviceId == deviceId, ct);
            List<DeviceDetectionType> types = await db.DeviceDetectionTypes.AsNoTracking().Where(t => t.DeviceId == deviceId).OrderBy(t => t.DetectionType).ToListAsync(ct);
            List<DeviceDetectionThreshold> thresholds = await db.DeviceDetectionThresholds.AsNoTracking().Where(t => t.DeviceId == deviceId).OrderBy(t => t.DetectionType).ToListAsync(ct);
            return new DeviceTypedSettings(offline, types, thresholds);
        }
    }
}
