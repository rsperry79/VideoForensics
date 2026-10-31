using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
            // One timestamp per upsert, so every change row written by this call shares the same observation time.
            DateTime changedAtUtc = DateTime.UtcNow;
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
                        existingFeatures.CfesEligible = features.CfesEligible;
                        existingFeatures.PackageWarning = features.PackageWarning;
                        existingFeatures.AutoZoomTrack = features.AutoZoomTrack;
                        existingFeatures.AutomatedSiren = features.AutomatedSiren;
                        existingFeatures.AlexaPlusGreetings = features.AlexaPlusGreetings;
                        existingFeatures.RemoteAccessControl = features.RemoteAccessControl;
                        existingFeatures.AutoShutoffDevice = features.AutoShutoffDevice;
                        existingFeatures.ShortOnLongOff = features.ShortOnLongOff;
                        existingFeatures.Tracklight = features.Tracklight;
                        existingFeatures.MotionZoneRecommendation = features.MotionZoneRecommendation;
                        existingFeatures.RichNotificationsEligible = features.RichNotificationsEligible;
                        existingFeatures.ShowOfflineMotionEvents = features.ShowOfflineMotionEvents;
                        existingFeatures.SheilaCameraEligible = features.SheilaCameraEligible;
                        existingFeatures.SheilaCameraProcessingEligible = features.SheilaCameraProcessingEligible;
                        existingFeatures.DynamicNetworkSwitchingEligible = features.DynamicNetworkSwitchingEligible;
                        existingFeatures.VehicleDetectionEligible = features.VehicleDetectionEligible;
                        existingFeatures.AnimalDetectionEligible = features.AnimalDetectionEligible;
                        existingFeatures.Show24x7Lite = features.Show24x7Lite;
                        existingFeatures.Recording24x7Eligible = features.Recording24x7Eligible;
                        existingFeatures.PropertyView = features.PropertyView;
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
                    await ReplaceSettingsChildrenAsync(db, deviceId, settings, changedAtUtc, ct);
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
        /// Changed children are appended to the change history in the same unit of work as the replacement.
        /// </summary>
        /// <summary>
        /// Replaces the typed offline-motion, detection-type and threshold rows. A null list leaves that table untouched;
        /// an empty list clears it. Changed rows are recorded in the setting-change log; first-time population is not.
        /// </summary>
        private static async Task<List<DeviceSettingChange>> ReplaceTypedSettingsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, DateTime changedAtUtc, CancellationToken ct)
        {
            // Stored rows are read before they are removed, so each change row compares the old state with the incoming one.
            var changes = new List<DeviceSettingChange>();
            if (settings.OfflineMotion != null)
            {
                List<DeviceOfflineMotionSettings> storedOffline = await db.DeviceOfflineMotionSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct);
                AddWholeItemChanges(changes, deviceId, changedAtUtc, "OfflineMotion",
                    ToMap(storedOffline, _ => "settings", OfflineMotionText),
                    ToMap(settings.OfflineMotion, _ => "settings", OfflineMotionText));
                db.DeviceOfflineMotionSettings.RemoveRange(storedOffline);
                foreach (DeviceOfflineMotionSettings row in settings.OfflineMotion)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.DeviceOfflineMotionSettings.Add(row);
                }
            }

            if (settings.DetectionTypes != null)
            {
                List<DeviceDetectionType> storedTypes = await db.DeviceDetectionTypes.Where(t => t.DeviceId == deviceId).ToListAsync(ct);
                AddWholeItemChanges(changes, deviceId, changedAtUtc, "DetectionType",
                    ToMap(storedTypes, t => t.DetectionType, DetectionTypeText),
                    ToMap(settings.DetectionTypes, t => t.DetectionType, DetectionTypeText));
                db.DeviceDetectionTypes.RemoveRange(storedTypes);
                foreach (DeviceDetectionType row in settings.DetectionTypes)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.DeviceDetectionTypes.Add(row);
                }
            }

            if (settings.DetectionThresholds != null)
            {
                List<DeviceDetectionThreshold> storedThresholds = await db.DeviceDetectionThresholds.Where(t => t.DeviceId == deviceId).ToListAsync(ct);
                AddWholeItemChanges(changes, deviceId, changedAtUtc, "DetectionThreshold",
                    ToMap(storedThresholds, t => t.DetectionType, t => Text(t.Threshold) ?? string.Empty),
                    ToMap(settings.DetectionThresholds ?? [], t => t.DetectionType, t => Text(t.Threshold) ?? string.Empty));
                db.DeviceDetectionThresholds.RemoveRange(storedThresholds);
                foreach (DeviceDetectionThreshold row in settings.DetectionThresholds)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.DeviceDetectionThresholds.Add(row);
                }
            }

            return changes;
        }

        /// <summary>
        /// Replaces the device's feature-state rows (and their reason rows) with the given complete list. A null list leaves
        /// stored rows untouched; an empty list clears them. Feature states are not change-logged.
        /// </summary>
        private static async Task ReplaceFeatureStatesAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.FeatureStates == null)
            {
                return;
            }

            List<RingDeviceFeatureState> stored = await db.RingDeviceFeatureStates.Where(s => s.DeviceId == deviceId).ToListAsync(ct);
            List<Guid> storedIds = stored.Select(s => s.Id).ToList();
            db.RingDeviceFeatureReasons.RemoveRange(await db.RingDeviceFeatureReasons.Where(r => storedIds.Contains(r.FeatureStateId)).ToListAsync(ct));
            db.RingDeviceFeatureStates.RemoveRange(stored);
            // EF orders the deletes ahead of the inserts, so re-adding a replaced (DeviceId, FeatureKey) does not hit the unique index.

            foreach (RingDeviceFeatureState state in settings.FeatureStates)
            {
                state.Id = state.Id == Guid.Empty ? Guid.NewGuid() : state.Id;
                state.DeviceId = deviceId;
                foreach (RingDeviceFeatureReason reason in state.Reasons)
                {
                    reason.Id = reason.Id == Guid.Empty ? Guid.NewGuid() : reason.Id;
                    reason.FeatureStateId = state.Id;
                }

                _ = db.RingDeviceFeatureStates.Add(state);
            }
        }

        /// <summary>
        /// Replaces the device's reported-health row and battery rows. A null health leaves stored rows untouched; a health row
        /// with a null battery list clears the batteries (Ring sent the object without a batteries array). Not change-logged.
        /// </summary>
        private static async Task ReplaceReportedHealthAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.ReportedHealth == null)
            {
                return;
            }

            db.DeviceReportedHealths.RemoveRange(await db.DeviceReportedHealths.Where(h => h.DeviceId == deviceId).ToListAsync(ct));
            db.DeviceReportedBatteries.RemoveRange(await db.DeviceReportedBatteries.Where(b => b.DeviceId == deviceId).ToListAsync(ct));
            db.RingDeviceHealthExtras.RemoveRange(await db.RingDeviceHealthExtras.Where(h => h.DeviceId == deviceId).ToListAsync(ct));
            // EF orders the deletes ahead of the inserts, so the unique (DeviceId) and (DeviceId, Position) indexes are not hit.

            DeviceReportedHealth health = settings.ReportedHealth;
            health.Id = health.Id == Guid.Empty ? Guid.NewGuid() : health.Id;
            health.DeviceId = deviceId;
            health.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.DeviceReportedHealths.Add(health);

            if (settings.RingHealthExtras != null)
            {
                RingDeviceHealthExtras extras = settings.RingHealthExtras;
                extras.Id = extras.Id == Guid.Empty ? Guid.NewGuid() : extras.Id;
                extras.DeviceId = deviceId;
                extras.UpdatedAtUtc = health.UpdatedAtUtc;
                _ = db.RingDeviceHealthExtras.Add(extras);
            }

            foreach (DeviceReportedBattery battery in settings.ReportedBatteries ?? [])
            {
                battery.Id = battery.Id == Guid.Empty ? Guid.NewGuid() : battery.Id;
                battery.DeviceId = deviceId;
                _ = db.DeviceReportedBatteries.Add(battery);
            }
        }

        /// <summary>Replaces the device's Ring-only top-level detail row. A null row leaves the stored row untouched.</summary>
        private static async Task ReplaceRingDetailsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.RingDetails == null)
            {
                return;
            }

            db.RingDeviceDetails.RemoveRange(await db.RingDeviceDetails.Where(r => r.DeviceId == deviceId).ToListAsync(ct));
            RingDeviceDetails row = settings.RingDetails;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.RingDeviceDetails.Add(row);
        }

        /// <summary>Replaces the device's Ring paid-feature row. A null row leaves the stored row untouched; an empty one writes a row of nulls.</summary>
        private static async Task ReplacePaidFeaturesAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.PaidFeatures == null)
            {
                return;
            }

            db.RingDevicePaidFeatures.RemoveRange(await db.RingDevicePaidFeatures.Where(p => p.DeviceId == deviceId).ToListAsync(ct));
            RingDevicePaidFeatures row = settings.PaidFeatures;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.RingDevicePaidFeatures.Add(row);
        }

        /// <summary>Replaces the device's network-settings row. A null row leaves the stored row untouched; an empty one writes a row of nulls.</summary>
        private static async Task ReplaceNetworkSettingsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.NetworkSettings == null)
            {
                return;
            }

            db.DeviceNetworkSettings.RemoveRange(await db.DeviceNetworkSettings.Where(n => n.DeviceId == deviceId).ToListAsync(ct));
            DeviceNetworkSettings row = settings.NetworkSettings;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.DeviceNetworkSettings.Add(row);
        }

        /// <summary>Replaces the device's Ring network-diagnosis row. A null row leaves the stored row untouched; an empty one writes a row of nulls.</summary>
        private static async Task ReplaceNetworkDiagnosisAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.NetworkDiagnosis == null)
            {
                return;
            }

            db.RingDeviceNetworkDiagnosis.RemoveRange(await db.RingDeviceNetworkDiagnosis.Where(d => d.DeviceId == deviceId).ToListAsync(ct));
            RingDeviceNetworkDiagnosis row = settings.NetworkDiagnosis;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.RingDeviceNetworkDiagnosis.Add(row);
        }

        /// <summary>Replaces the device's Ring settings row. A null row leaves the stored row untouched; an empty one writes a row of nulls.</summary>
        private static async Task ReplaceRingSettingsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.RingSettings == null)
            {
                return;
            }

            db.RingDeviceSettings.RemoveRange(await db.RingDeviceSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
            RingDeviceSettings row = settings.RingSettings;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.RingDeviceSettings.Add(row);
        }

        /// <summary>Replaces the device's general settings row. A null row leaves the stored row untouched; an empty one writes a row of nulls.</summary>
        private static async Task ReplaceGeneralSettingsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            if (settings.GeneralSettings == null)
            {
                return;
            }

            db.DeviceGeneralSettings.RemoveRange(await db.DeviceGeneralSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
            DeviceGeneralSettings row = settings.GeneralSettings;
            row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
            row.DeviceId = deviceId;
            row.UpdatedAtUtc = DateTime.UtcNow;
            _ = db.DeviceGeneralSettings.Add(row);
        }

        /// <summary>Replaces the device's endpoint setting group rows. A null group or list leaves the stored rows untouched; a non-null group writes its row, nulls included, and a list replaces the stored rows.</summary>
        private static async Task ReplaceSettingGroupRowsAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, CancellationToken ct)
        {
            RingDeviceSettingGroupRows? g = settings.RingSettingGroups;
            if (g == null)
            {
                return;
            }

            if (g.AdvancedMotion != null)
            {
                db.RingDeviceAdvancedMotionSettings.RemoveRange(await db.RingDeviceAdvancedMotionSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceAdvancedMotionSettings row = g.AdvancedMotion;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceAdvancedMotionSettings.Add(row);
            }

            if (g.Backend != null)
            {
                db.RingDeviceBackendSettings.RemoveRange(await db.RingDeviceBackendSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceBackendSettings row = g.Backend;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceBackendSettings.Add(row);
            }

            if (g.ClientDevice != null)
            {
                db.RingDeviceClientDeviceSettings.RemoveRange(await db.RingDeviceClientDeviceSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceClientDeviceSettings row = g.ClientDevice;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceClientDeviceSettings.Add(row);
            }

            if (g.Cv != null)
            {
                db.RingDeviceCvSettings.RemoveRange(await db.RingDeviceCvSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceCvSettings row = g.Cv;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceCvSettings.Add(row);
            }

            if (g.General != null)
            {
                db.RingDeviceGeneralSettings.RemoveRange(await db.RingDeviceGeneralSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceGeneralSettings row = g.General;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceGeneralSettings.Add(row);
            }

            if (g.Misc != null)
            {
                db.RingDeviceMiscSettings.RemoveRange(await db.RingDeviceMiscSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceMiscSettings row = g.Misc;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceMiscSettings.Add(row);
            }

            if (g.Motion != null)
            {
                db.RingDeviceMotionSettings.RemoveRange(await db.RingDeviceMotionSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceMotionSettings row = g.Motion;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceMotionSettings.Add(row);
            }

            if (g.Snapshot != null)
            {
                db.RingDeviceSnapshotSettings.RemoveRange(await db.RingDeviceSnapshotSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceSnapshotSettings row = g.Snapshot;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceSnapshotSettings.Add(row);
            }

            if (g.Stream != null)
            {
                db.RingDeviceStreamSettings.RemoveRange(await db.RingDeviceStreamSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceStreamSettings row = g.Stream;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceStreamSettings.Add(row);
            }

            if (g.Video != null)
            {
                db.RingDeviceVideoSettings.RemoveRange(await db.RingDeviceVideoSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceVideoSettings row = g.Video;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceVideoSettings.Add(row);
            }

            if (g.Vod != null)
            {
                db.RingDeviceVodSettings.RemoveRange(await db.RingDeviceVodSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct));
                RingDeviceVodSettings row = g.Vod;
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.DeviceId = deviceId;
                row.UpdatedAtUtc = DateTime.UtcNow;
                _ = db.RingDeviceVodSettings.Add(row);
            }
            if (g.MotionSnoozeProfiles != null)
            {
                db.RingDeviceMotionSnoozeProfiles.RemoveRange(await db.RingDeviceMotionSnoozeProfiles.Where(p => p.DeviceId == deviceId).ToListAsync(ct));
                foreach (RingDeviceMotionSnoozeProfile row in g.MotionSnoozeProfiles)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.RingDeviceMotionSnoozeProfiles.Add(row);
                }
            }
            if (g.CvThresholdVersions != null)
            {
                db.RingDeviceCvThresholdOverrideVersions.RemoveRange(await db.RingDeviceCvThresholdOverrideVersions.Where(p => p.DeviceId == deviceId).ToListAsync(ct));
                foreach (RingDeviceCvThresholdOverrideVersion row in g.CvThresholdVersions)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.RingDeviceCvThresholdOverrideVersions.Add(row);
                }
            }
            if (g.CvThresholdValues != null)
            {
                db.RingDeviceCvThresholdOverrideValues.RemoveRange(await db.RingDeviceCvThresholdOverrideValues.Where(p => p.DeviceId == deviceId).ToListAsync(ct));
                foreach (RingDeviceCvThresholdOverrideValue row in g.CvThresholdValues)
                {
                    row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                    row.DeviceId = deviceId;
                    _ = db.RingDeviceCvThresholdOverrideValues.Add(row);
                }
            }
        }

        /// <summary>Text compared for an offline motion change; every stored field is included so any field change is recorded.</summary>
        private static string OfflineMotionText(DeviceOfflineMotionSettings s)
        {
            return $"subscribed={Text(s.Subscribed)};enabled={Text(s.Enabled)};maxUploadKb={Text(s.MaxUploadKb)};resolutionP={Text(s.ResolutionP)};frequencyAfterSecs={Text(s.FrequencyAfterSecs)};periodAfterSecs={Text(s.PeriodAfterSecs)}";
        }

        /// <summary>Text compared for a detection-type change: the enabled, mode, record and notification flags.</summary>
        private static string DetectionTypeText(DeviceDetectionType t)
        {
            return $"enabled={Text(t.Enabled)};mode={t.Mode};record={Text(t.Record)};notification={Text(t.Notification)}";
        }

        private static async Task ReplaceSettingsChildrenAsync(VideoForensicsDbContext db, Guid deviceId, DeviceSettingsChildren settings, DateTime changedAtUtc, CancellationToken ct)
        {
            // Stored rows are read before anything is removed, so the change rows compare the old state with the incoming one.
            List<DeviceMotionSnoozePreset> storedSnooze = await db.DeviceMotionSnoozePresets.Where(p => p.DeviceId == deviceId).ToListAsync(ct);
            List<DeviceLiveViewPreset> storedLive = await db.DeviceLiveViewPresets.Where(p => p.DeviceId == deviceId).ToListAsync(ct);
            List<DeviceAdvancedMotionZone> existingZones = await db.DeviceAdvancedMotionZones
                .Include(z => z.Vertices)
                .Where(z => z.DeviceId == deviceId)
                .ToListAsync(ct);

            // Zones and object settings are only read, compared and replaced when the caller supplied them (null leaves stored rows as they are).
            List<DeviceZone> existingZoneRows = [];
            if (settings.Zones != null)
            {
                existingZoneRows = await db.DeviceZones
                    .Include(z => z.DetectionTypes)
                    .Include(z => z.Vertices)
                    .Where(z => z.DeviceId == deviceId)
                    .ToListAsync(ct);
            }

            List<DeviceAdvancedObjectSetting> existingObjects = [];
            if (settings.AdvancedObjectSettings != null)
            {
                existingObjects = await db.DeviceAdvancedObjectSettings.Where(s => s.DeviceId == deviceId).ToListAsync(ct);
            }

            var changes = new List<DeviceSettingChange>();
            AddWholeItemChanges(changes, deviceId, changedAtUtc, "MotionSnoozePreset",
                ToMap(storedSnooze, p => PositionKey(p.Position), p => p.Preset),
                ToMap(settings.MotionSnoozePresets, p => PositionKey(p.Position), p => p.Preset));
            AddWholeItemChanges(changes, deviceId, changedAtUtc, "LiveViewPreset",
                ToMap(storedLive, p => PositionKey(p.Position), p => p.Preset),
                ToMap(settings.LiveViewPresets, p => PositionKey(p.Position), p => p.Preset));
            AddWholeItemChanges(changes, deviceId, changedAtUtc, "AdvancedMotionZone",
                ToMap(existingZones, z => z.ZoneKey, MotionZoneText),
                ToMap(settings.AdvancedMotionZones, z => z.ZoneKey, MotionZoneText));
            if (settings.Zones != null)
            {
                AddZoneChanges(changes, deviceId, changedAtUtc, existingZoneRows, settings.Zones);
            }

            if (settings.AdvancedObjectSettings != null)
            {
                AddWholeItemChanges(changes, deviceId, changedAtUtc, "AdvancedObjectSetting",
                    ToMap(existingObjects, s => s.SettingKey, ObjectSettingText),
                    ToMap(settings.AdvancedObjectSettings, s => s.SettingKey, ObjectSettingText));
            }

            db.DeviceSettingChanges.AddRange(changes);
            db.DeviceSettingChanges.AddRange(await ReplaceTypedSettingsAsync(db, deviceId, settings, changedAtUtc, ct));
            await ReplaceFeatureStatesAsync(db, deviceId, settings, ct);
            await ReplaceReportedHealthAsync(db, deviceId, settings, ct);
            await ReplaceRingDetailsAsync(db, deviceId, settings, ct);
            await ReplacePaidFeaturesAsync(db, deviceId, settings, ct);
            await ReplaceNetworkSettingsAsync(db, deviceId, settings, ct);
            await ReplaceNetworkDiagnosisAsync(db, deviceId, settings, ct);
            await ReplaceRingSettingsAsync(db, deviceId, settings, ct);
            await ReplaceGeneralSettingsAsync(db, deviceId, settings, ct);
            await ReplaceSettingGroupRowsAsync(db, deviceId, settings, ct);
            // An Observed light event is written only on an off-to-on transition, in the same save as the rest of the sync.
            _ = await DeviceLightEventRepository.AddObservedIfTransitionAsync(db, deviceId, settings.LightSecondsRemaining, changedAtUtc, ct);
            if (settings.LightIsOn is { } lightOn)
            {
                _ = await DeviceLightEventRepository.AddObservedStateIfChangedAsync(db, deviceId, lightOn, changedAtUtc, ct);
            }

            db.DeviceMotionSnoozePresets.RemoveRange(storedSnooze);
            db.DeviceLiveViewPresets.RemoveRange(storedLive);
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

            if (settings.Zones != null)
            {
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
                db.DeviceAdvancedObjectSettings.RemoveRange(existingObjects);
                foreach (DeviceAdvancedObjectSetting setting in settings.AdvancedObjectSettings)
                {
                    setting.Id = setting.Id == Guid.Empty ? Guid.NewGuid() : setting.Id;
                    setting.DeviceId = deviceId;
                    _ = db.DeviceAdvancedObjectSettings.Add(setting);
                }
            }
        }

        // ---- Change history -------------------------------------------------------------------------------------
        // Rules: a collection with no stored rows is first population, not a change, so it records nothing. Otherwise
        // each natural key present on one side only is an add (OldValue null) or removal (NewValue null), and a key on
        // both sides records a change only when its canonical text differs. Zones record one row per changed property.

        /// <summary>Longest FieldName the DeviceSettingChange column accepts.</summary>
        private const int MaxFieldNameLength = 64;

        private static readonly (string Suffix, Func<DeviceZone, string?> Text)[] ZoneProperties =
        [
            (".Source", z => z.Source),
            (".Name", z => z.Name),
            (".State", z => Text(z.State)),
            (".Style", z => z.Style),
            (".DetectionTypes", z => string.Join(",", z.DetectionTypes.OrderBy(t => t.Position).Select(t => t.DetectionType))),
            (".Vertices", z => VerticesText(z.Vertices.Select(v => (v.Position, v.X, v.Y))))
        ];

        private static void AddWholeItemChanges(
            List<DeviceSettingChange> changes,
            Guid deviceId,
            DateTime changedAtUtc,
            string collection,
            Dictionary<string, string> stored,
            Dictionary<string, string> incoming)
        {
            if (stored.Count == 0)
            {
                return;
            }

            foreach (string key in stored.Keys.Union(incoming.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool inStored = stored.TryGetValue(key, out string? before);
                bool inIncoming = incoming.TryGetValue(key, out string? after);
                if (inStored && inIncoming && before == after)
                {
                    continue;
                }

                changes.Add(NewChange(deviceId, changedAtUtc, FieldName(collection, key, null), inStored ? before : null, inIncoming ? after : null));
            }
        }

        private static void AddZoneChanges(
            List<DeviceSettingChange> changes,
            Guid deviceId,
            DateTime changedAtUtc,
            List<DeviceZone> stored,
            IReadOnlyList<DeviceZone> incoming)
        {
            if (stored.Count == 0)
            {
                return;
            }

            Dictionary<string, DeviceZone> storedByKey = ToMap(stored, ZoneKey, z => z);
            Dictionary<string, DeviceZone> incomingByKey = ToMap(incoming, ZoneKey, z => z);
            foreach (string key in storedByKey.Keys.Union(incomingByKey.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool inStored = storedByKey.TryGetValue(key, out DeviceZone? before);
                bool inIncoming = incomingByKey.TryGetValue(key, out DeviceZone? after);
                if (inStored && inIncoming)
                {
                    foreach ((string suffix, Func<DeviceZone, string?> text) in ZoneProperties)
                    {
                        string? oldText = text(before!);
                        string? newText = text(after!);
                        if (oldText != newText)
                        {
                            changes.Add(NewChange(deviceId, changedAtUtc, FieldName("Zone", key, suffix), oldText, newText));
                        }
                    }
                }
                else
                {
                    changes.Add(NewChange(
                        deviceId,
                        changedAtUtc,
                        FieldName("Zone", key, null),
                        inStored ? ZoneText(before!) : null,
                        inIncoming ? ZoneText(after!) : null));
                }
            }
        }

        private static DeviceSettingChange NewChange(Guid deviceId, DateTime changedAtUtc, string fieldName, string? oldValue, string? newValue)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedAtUtc = changedAtUtc
            };
        }

        /// <summary>
        /// Builds "Collection[key]" plus an optional property suffix. A key that would overflow the column is cut and
        /// suffixed with a short hash of the full key, so two long keys that share a prefix still get distinct names.
        /// </summary>
        private static string FieldName(string collection, string key, string? suffix)
        {
            string tail = suffix ?? string.Empty;
            int budget = MaxFieldNameLength - (collection.Length + "[]".Length + tail.Length);
            string safeKey = key.Length <= budget ? key : TruncateKey(key, budget);
            return $"{collection}[{safeKey}]{tail}";
        }

        private static string TruncateKey(string key, int budget)
        {
            const int HashLength = 8;
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..HashLength];
            return $"{key[..(budget - HashLength - 1)]}~{hash}";
        }

        private static Dictionary<string, TValue> ToMap<T, TValue>(IEnumerable<T> items, Func<T, string> keyOf, Func<T, TValue> valueOf)
        {
            var map = new Dictionary<string, TValue>(StringComparer.Ordinal);
            foreach (T item in items)
            {
                map[keyOf(item)] = valueOf(item);
            }

            return map;
        }

        private static string PositionKey(int position)
        {
            return position.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Zone natural key: the Ring zone id, falling back to the name when Ring sends no id.</summary>
        private static string ZoneKey(DeviceZone zone)
        {
            return zone.ZoneId ?? zone.Name ?? string.Empty;
        }

        private static string ZoneText(DeviceZone zone)
        {
            return string.Join("|", ZoneProperties.Select(p => $"{p.Suffix.TrimStart('.')}={p.Text(zone)}"));
        }

        private static string MotionZoneText(DeviceAdvancedMotionZone zone)
        {
            return $"name={zone.Name}|state={Text(zone.State)}|vertices={VerticesText(zone.Vertices.Select(v => (v.Position, v.X, v.Y)))}";
        }

        private static string ObjectSettingText(DeviceAdvancedObjectSetting setting)
        {
            return $"day={Text(setting.Day)}|night={Text(setting.Night)}";
        }

        /// <summary>Vertices as "x,y" pairs in position order. Pairs are joined with ';' because a bare ',' join cannot tell a missing Y from a pair boundary.</summary>
        private static string VerticesText(IEnumerable<(int Position, double? X, double? Y)> vertices)
        {
            return string.Join(";", vertices.OrderBy(v => v.Position).Select(v => $"{Text(v.X)},{Text(v.Y)}"));
        }

        /// <summary>Invariant-culture text for a numeric value, or null when the value is null.</summary>
        private static string? Text(object? value)
        {
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
