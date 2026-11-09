using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Reads a device's typed settings rows (offline motion, detection types, thresholds).</summary>
    public class DeviceTypedSettingsReader : IDeviceTypedSettingsReader
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeviceTypedSettingsReader(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public async Task<DeviceTypedSettings> GetAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                DeviceOfflineMotionSettings? offline = await db.DeviceOfflineMotionSettings.AsNoTracking().FirstOrDefaultAsync(s => s.DeviceId == deviceId, ct);
                List<DeviceDetectionType> types = await db.DeviceDetectionTypes.AsNoTracking().Where(t => t.DeviceId == deviceId).OrderBy(t => t.DetectionType).ToListAsync(ct);
                List<DeviceDetectionThreshold> thresholds = await db.DeviceDetectionThresholds.AsNoTracking().Where(t => t.DeviceId == deviceId).OrderBy(t => t.DetectionType).ToListAsync(ct);
                return new DeviceTypedSettings(offline, types, thresholds);
            }, ct);
        }
    }
}
