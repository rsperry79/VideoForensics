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
            CancellationToken ct,
            DeviceSettingsChildren? settings = null)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Device? device = null;
            try
            {
                device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
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
                        existingFeatures.ShowVodSettings = features.ShowVodSettings;
                        existingFeatures.RingtonesEnabled = features.RingtonesEnabled;
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
                            existingAlert.AlertValue = alert.AlertValue;
                        }
                    }
                }

                if (settings != null)
                {
                    await ReplaceSettingsChildrenAsync(db, deviceId, settings, ct);
                }

                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Device details recorded for device {DeviceName}", device.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording device details for device {DeviceName}", device?.Name ?? "unknown device");
                throw;
            }
        }

        /// <summary>
        /// Replaces the device's preset and advanced-zone rows with the given complete lists. Replacing (rather than
        /// merging) means a preset or zone Ring no longer reports is removed, not left behind as a stale row.
        /// </summary>
        private static async Task ReplaceSettingsChildrenAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            db.DeviceMotionSnoozePresets.RemoveRange(await db.DeviceMotionSnoozePresets.Where(p => p.DeviceId == deviceId).ToListAsync(ct));
            db.DeviceLiveViewPresets.RemoveRange(await db.DeviceLiveViewPresets.Where(p => p.DeviceId == deviceId).ToListAsync(ct));

            List<DeviceAdvancedMotionZone> existingZones = await db.DeviceAdvancedMotionZones
                .Include(z => z.Vertices)
                .Where(z => z.DeviceId == deviceId)
                .ToListAsync(ct);
            db.DeviceAdvancedMotionZoneVertices.RemoveRange(existingZones.SelectMany(z => z.Vertices));
            db.DeviceAdvancedMotionZones.RemoveRange(existingZones);

            foreach (DeviceMotionSnoozePreset preset in settings.MotionSnoozePresets)
            {
                preset.Id = preset.Id == Guid.Empty ? Guid.NewGuid() : preset.Id;
                preset.DeviceId = deviceId;
                _ = db.DeviceMotionSnoozePresets.Add(preset);
            }

            foreach (DeviceLiveViewPreset preset in settings.LiveViewPresets)
            {
                preset.Id = preset.Id == Guid.Empty ? Guid.NewGuid() : preset.Id;
                preset.DeviceId = deviceId;
                _ = db.DeviceLiveViewPresets.Add(preset);
            }

            foreach (DeviceAdvancedMotionZone zone in settings.AdvancedMotionZones)
            {
                zone.Id = zone.Id == Guid.Empty ? Guid.NewGuid() : zone.Id;
                zone.DeviceId = deviceId;
                foreach (DeviceAdvancedMotionZoneVertex vertex in zone.Vertices)
                {
                    vertex.Id = vertex.Id == Guid.Empty ? Guid.NewGuid() : vertex.Id;
                    vertex.DeviceAdvancedMotionZoneId = zone.Id;
                }

                _ = db.DeviceAdvancedMotionZones.Add(zone);
            }

            // Zones and object settings are only replaced when the caller supplied them (null leaves stored rows as they are).
            if (settings.Zones != null)
            {
                List<DeviceZone> existingZoneRows = await db.DeviceZones
                    .Include(z => z.DetectionTypes)
                    .Include(z => z.Vertices)
                    .Where(z => z.DeviceId == deviceId)
                    .ToListAsync(ct);
                db.DeviceZoneDetectionTypes.RemoveRange(existingZoneRows.SelectMany(z => z.DetectionTypes));
                db.DeviceZoneVertices.RemoveRange(existingZoneRows.SelectMany(z => z.Vertices));
                db.DeviceZones.RemoveRange(existingZoneRows);

                foreach (DeviceZone zone in settings.Zones)
                {
                    zone.Id = zone.Id == Guid.Empty ? Guid.NewGuid() : zone.Id;
                    zone.DeviceId = deviceId;
                    foreach (DeviceZoneDetectionType detectionType in zone.DetectionTypes)
                    {
                        detectionType.Id = detectionType.Id == Guid.Empty ? Guid.NewGuid() : detectionType.Id;
                        detectionType.DeviceZoneId = zone.Id;
                    }

                    foreach (DeviceZoneVertex vertex in zone.Vertices)
                    {
                        vertex.Id = vertex.Id == Guid.Empty ? Guid.NewGuid() : vertex.Id;
                        vertex.DeviceZoneId = zone.Id;
                    }

                    _ = db.DeviceZones.Add(zone);
                }
            }

            if (settings.AdvancedObjectSettings != null)
            {
                db.DeviceAdvancedObjectSettings.RemoveRange(await db.DeviceAdvancedObjectSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                foreach (DeviceAdvancedObjectSetting setting in settings.AdvancedObjectSettings)
                {
                    setting.Id = setting.Id == Guid.Empty ? Guid.NewGuid() : setting.Id;
                    setting.DeviceId = deviceId;
                    _ = db.DeviceAdvancedObjectSettings.Add(setting);
                }
            }
        }
    }
}
