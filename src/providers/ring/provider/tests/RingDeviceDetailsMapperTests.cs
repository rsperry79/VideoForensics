using VideoForensics.Data.Common.Entities;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class RingDeviceDetailsMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        [Fact]
        public void Find_Doorbot_ReturnsTimeZoneFirmwareFeaturesAndAlerts()
        {
            var devices = new Devices
            {
                Doorbots = [new Doorbot
                {
                    Id = 123,
                    TimeZone = "America/Chicago",
                    FirmwareVersion = "2.0.1",
                    Features = new DoorbotFeatures { MotionsEnabled = true, NightVisionEnabled = true, ShowRecordings = false },
                    Alerts = new DoorbotAlerts { Connection = "online" }
                }]
            };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "123");

            Assert.NotNull(details);
            Assert.Equal("America/Chicago", details!.TimeZone);
            Assert.Equal("2.0.1", details.FirmwareVersion);
            Assert.True(details.HasFeatures);
            Assert.True(details.MotionsEnabled);
            Assert.True(details.NightVisionEnabled);
            Assert.False(details.ShowRecordings);
            Assert.Equal("online", details.AlertConnection);
            Assert.Null(details.HasAudio);
        }

        [Fact]
        public void Find_StickupCam_ReturnsAudioFromSettings()
        {
            var devices = new Devices
            {
                StickupCams = [new StickupCam
                {
                    Id = 456L,
                    TimeZone = "Europe/London",
                    Settings = new StickupCamSettings { EnableAudioRecording = true },
                    Features = new StickupCamFeatures { MotionsEnabled = false, ShowRecordings = true },
                    Alerts = new StickupCamAlerts { Connection = "offline" }
                }]
            };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "456");

            Assert.NotNull(details);
            Assert.True(details!.HasAudio);
            Assert.False(details.MotionsEnabled);
            Assert.Null(details.NightVisionEnabled);
            Assert.Equal("offline", details.AlertConnection);
        }

        [Fact]
        public void Find_Chime_ReturnsTimeZoneAndFirmwareWithoutFeatureRow()
        {
            var devices = new Devices
            {
                Chimes = [new Chime { Id = 789, TimeZone = "Asia/Tokyo", FirmwareVersion = "1.1.0", Features = new ChimeFeatures { RingtonesEnabled = true } }]
            };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "789");

            Assert.NotNull(details);
            Assert.Equal("Asia/Tokyo", details!.TimeZone);
            Assert.Equal("1.1.0", details.FirmwareVersion);
            Assert.False(details.HasFeatures);
            Assert.Null(details.AlertConnection);
        }

        [Fact]
        public void Find_UnknownDevice_ReturnsNull()
        {
            var devices = new Devices { Doorbots = [new Doorbot { Id = 1 }] };

            Assert.Null(RingDeviceDetailsMapper.Find(devices, "999"));
            Assert.Null(RingDeviceDetailsMapper.Find(null, "1"));
        }

        [Fact]
        public void ToCapabilities_MapsAudioMotionNightVisionAndFirmware_LeavesStorageNull()
        {
            var details = new RingDeviceDetails(
                TimeZone: null, FirmwareVersion: "2.0.1", HasAudio: true, MotionsEnabled: true, NightVisionEnabled: false,
                HasFeatures: true, ShowRecordings: null, AdvancedMotionEnabled: null, PeopleOnlyEnabled: null,
                ShadowCorrectionEnabled: null, MotionMessageEnabled: null, AlertConnection: null);
            var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

            DeviceCapabilities caps = RingDeviceDetailsMapper.ToCapabilities(details, DeviceId, now);

            Assert.Equal(DeviceId, caps.DeviceId);
            Assert.True(caps.HasAudio);
            Assert.True(caps.HasMotionDetection);
            Assert.False(caps.HasNightVision);
            Assert.Null(caps.HasCloudStorage);
            Assert.Null(caps.Resolution);
            Assert.Null(caps.StorageType);
            Assert.Equal("2.0.1", caps.FirmwareVersion);
            Assert.Equal(now, caps.LastSyncedUtc);
        }

        [Fact]
        public void ToFeatures_NoFeaturesObject_ReturnsNull()
        {
            var details = new RingDeviceDetails(null, null, null, null, null, false, null, null, null, null, null, null);

            Assert.Null(RingDeviceDetailsMapper.ToFeatures(details, DeviceId));
        }

        [Fact]
        public void ToFeatures_WithFeatures_MapsFlagsToDeviceId()
        {
            var details = new RingDeviceDetails(null, null, null, true, false, true, true, false, true, false, true, null);

            DeviceFeatures? features = RingDeviceDetailsMapper.ToFeatures(details, DeviceId);

            Assert.NotNull(features);
            Assert.Equal(DeviceId, features!.DeviceId);
            Assert.True(features.MotionsEnabled);
            Assert.False(features.NightVisionEnabled);
            Assert.True(features.ShowRecordings);
            Assert.False(features.AdvancedMotionEnabled);
            Assert.True(features.PeopleOnlyEnabled);
            Assert.False(features.ShadowCorrectionEnabled);
            Assert.True(features.MotionMessageEnabled);
        }

        [Fact]
        public void ToAlerts_WithConnection_ReturnsSingleConnectionRowWithUnknownEnabledState()
        {
            var details = new RingDeviceDetails(null, null, null, null, null, false, null, null, null, null, null, "online");

            IReadOnlyList<DeviceAlerts> alerts = RingDeviceDetailsMapper.ToAlerts(details, DeviceId);

            DeviceAlerts alert = Assert.Single(alerts);
            Assert.Equal(DeviceId, alert.DeviceId);
            Assert.Equal("connection", alert.AlertType);
            Assert.Null(alert.IsEnabled);
        }

        [Fact]
        public void ToAlerts_NoConnection_ReturnsEmpty()
        {
            var details = new RingDeviceDetails(null, null, null, null, null, false, null, null, null, null, null, null);

            Assert.Empty(RingDeviceDetailsMapper.ToAlerts(details, DeviceId));
        }

        [Fact]
        public void ToDeviceFields_Doorbot_MapsDeviceScalars()
        {
            var doorbot = new Doorbot
            {
                Id = 123,
                DeviceId = "aabbcc",
                Kind = "doorbell_v4",
                FirmwareVersion = "2.0.1",
                Latitude = 41.5,
                Longitude = -87.6,
                Address = "1 Main St",
                Subscribed = true,
                SubscribedMotions = false,
                BatteryLife = 87,
                ExternalConnection = true,
                Owned = true,
                Stolen = false
            };

            DeviceDetailFields? fields = RingDeviceDetailsMapper.ToDeviceFields(doorbot);

            Assert.NotNull(fields);
            Assert.Equal("aabbcc", fields!.RingDeviceId);
            Assert.Equal("doorbell_v4", fields.RingKind);
            Assert.Equal("2.0.1", fields.FirmwareVersion);
            Assert.Equal(41.5, fields.Latitude);
            Assert.Equal(-87.6, fields.Longitude);
            Assert.Equal("1 Main St", fields.Address);
            Assert.True(fields.Subscribed);
            Assert.False(fields.SubscribedMotions);
            Assert.Equal("87", fields.BatteryLife);
            Assert.True(fields.ExternalConnection);
            Assert.True(fields.Owned);
            Assert.False(fields.Stolen);
        }

        [Fact]
        public void ToDeviceFields_StickupCam_MapsBatteryLifeAsRawString()
        {
            var cam = new StickupCam
            {
                Id = 456L,
                DeviceId = "ddeeff",
                Kind = "stickup_cam_v4",
                BatteryLife = 64L,
                Subscribed = false,
                Stolen = true
            };

            DeviceDetailFields? fields = RingDeviceDetailsMapper.ToDeviceFields(cam);

            Assert.NotNull(fields);
            Assert.Equal("ddeeff", fields!.RingDeviceId);
            Assert.Equal("stickup_cam_v4", fields.RingKind);
            Assert.Equal("64", fields.BatteryLife);
            Assert.False(fields.Subscribed);
            Assert.True(fields.Stolen);
        }

        [Fact]
        public void ToDeviceFields_Chime_MapsOnlyFieldsChimeHas()
        {
            var chime = new Chime
            {
                Id = 789,
                DeviceId = "112233",
                Kind = "chime_v2",
                FirmwareVersion = "1.1.0"
            };

            DeviceDetailFields? fields = RingDeviceDetailsMapper.ToDeviceFields(chime);

            Assert.NotNull(fields);
            Assert.Equal("112233", fields!.RingDeviceId);
            Assert.Equal("chime_v2", fields.RingKind);
            Assert.Equal("1.1.0", fields.FirmwareVersion);
            Assert.Null(fields.Subscribed);
            Assert.Null(fields.SubscribedMotions);
            Assert.Null(fields.BatteryLife);
            Assert.Null(fields.ExternalConnection);
        }

        [Fact]
        public void ToDeviceFields_MissingSourceValues_StayNull()
        {
            var doorbot = new Doorbot { Id = 1 };

            DeviceDetailFields? fields = RingDeviceDetailsMapper.ToDeviceFields(doorbot);

            Assert.NotNull(fields);
            Assert.Null(fields!.RingDeviceId);
            Assert.Null(fields.RingKind);
            Assert.Null(fields.FirmwareVersion);
            Assert.Null(fields.Latitude);
            Assert.Null(fields.Longitude);
            Assert.Null(fields.Address);
            Assert.Null(fields.Subscribed);
            Assert.Null(fields.SubscribedMotions);
            Assert.Null(fields.BatteryLife);
            Assert.Null(fields.ExternalConnection);
            Assert.Null(fields.Owned);
            Assert.Null(fields.Stolen);
        }

        [Fact]
        public void Find_Doorbot_ExposesDeviceFields()
        {
            var devices = new Devices
            {
                Doorbots = [new Doorbot { Id = 123, DeviceId = "aabbcc", Owned = true }]
            };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(devices, "123");

            Assert.NotNull(details?.DeviceFields);
            Assert.Equal("aabbcc", details!.DeviceFields!.RingDeviceId);
            Assert.True(details.DeviceFields.Owned);
        }
    }
}
