using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Upserts a device's capability, feature and alert rows in one transaction.</summary>
    public class DeviceDetailsRepository : IDeviceDetailsRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceDetailsRepository> _logger;

        public DeviceDetailsRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceDetailsRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task UpsertAsync(
            Guid deviceId,
            Guid? healthId,
            DeviceCapabilities? capabilities,
            DeviceFeatures? features,
            IReadOnlyList<DeviceAlerts>? alerts,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Device? device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                if (device == null)
                {
                    _logger.LogWarning("Device details not written: device {DeviceId} does not exist", deviceId);
                    return;
                }

                if (healthId.HasValue)
                {
                    device.DeviceHealthId = healthId.Value;
                }

                if (capabilities != null)
                {
                    DeviceCapabilities? existingCaps = await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.DeviceId == deviceId, ct);
                    if (existingCaps == null)
                    {
                        capabilities.Id = capabilities.Id == Guid.Empty ? Guid.NewGuid() : capabilities.Id;
                        capabilities.DeviceId = deviceId;
                        _ = db.DeviceCapabilities.Add(capabilities);
                    }
                    else
                    {
                        existingCaps.HasAudio = capabilities.HasAudio;
                        existingCaps.HasMotionDetection = capabilities.HasMotionDetection;
                        existingCaps.HasNightVision = capabilities.HasNightVision;
                        existingCaps.HasCloudStorage = capabilities.HasCloudStorage;
                        existingCaps.Resolution = capabilities.Resolution;
                        existingCaps.StorageType = capabilities.StorageType;
                        existingCaps.MaxStorageDays = capabilities.MaxStorageDays;
                        existingCaps.FirmwareVersion = capabilities.FirmwareVersion;
                        existingCaps.HardwareModel = capabilities.HardwareModel;
                        existingCaps.LastSyncedUtc = capabilities.LastSyncedUtc;
                        existingCaps.SyncStatus = capabilities.SyncStatus;
                    }
                }

                if (features != null)
                {
                    DeviceFeatures? existingFeatures = await db.DeviceFeatures.FirstOrDefaultAsync(f => f.DeviceId == deviceId, ct);
                    if (existingFeatures == null)
                    {
                        features.Id = features.Id == Guid.Empty ? Guid.NewGuid() : features.Id;
                        features.DeviceId = deviceId;
                        _ = db.DeviceFeatures.Add(features);
                        existingFeatures = features;
                    }
                    else
                    {
                        existingFeatures.MotionsEnabled = features.MotionsEnabled;
                        existingFeatures.ShowRecordings = features.ShowRecordings;
                        existingFeatures.AdvancedMotionEnabled = features.AdvancedMotionEnabled;
                        existingFeatures.PeopleOnlyEnabled = features.PeopleOnlyEnabled;
                        existingFeatures.ShadowCorrectionEnabled = features.ShadowCorrectionEnabled;
                        existingFeatures.MotionMessageEnabled = features.MotionMessageEnabled;
                        existingFeatures.NightVisionEnabled = features.NightVisionEnabled;
                    }

                    device.DeviceFeaturesId = existingFeatures.Id;
                }

                if (alerts != null)
                {
                    foreach (DeviceAlerts alert in alerts)
                    {
                        DeviceAlerts? existingAlert = await db.DeviceAlerts
                            .FirstOrDefaultAsync(a => a.DeviceId == deviceId && a.AlertType == alert.AlertType, ct);
                        if (existingAlert == null)
                        {
                            alert.Id = alert.Id == Guid.Empty ? Guid.NewGuid() : alert.Id;
                            alert.DeviceId = deviceId;
                            _ = db.DeviceAlerts.Add(alert);
                        }
                        else
                        {
                            existingAlert.IsEnabled = alert.IsEnabled;
                        }
                    }
                }

                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Device details recorded for device {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording device details for device {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
