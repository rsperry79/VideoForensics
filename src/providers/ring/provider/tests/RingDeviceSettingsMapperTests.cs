using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Maps the device settings, owner, chime and feature fields that the Ring device JSON carries.</summary>
    public class RingDeviceSettingsMapperTests
    {
        private static readonly Guid DeviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static DeviceDetailFields? StickupFields(StickupCamSettings? settings, Owner? owner = null)
        {
            var devices = new Devices { StickupCams = [new StickupCam { Id = 456L, Settings = settings, Owner = owner }] };
            return RingDeviceDetailsMapper.Find(devices, "456")?.DeviceFields;
        }

        [Fact]
        public void ToDeviceFields_StickupCamSettings_MapsScalarSettings()
        {
            DeviceDetailFields? f = StickupFields(new StickupCamSettings
            {
                EnableVod = true,
                ExposureControl = 3,
                MotionSnoozePresetProfile = "custom",
                LiveViewPresetProfile = "high",
                PirSensitivity1 = 7,
                VodSuspended = 1,
                DoorbellVolume = 5,
                VodStatus = "active",
                AdvancedMotionDetectionEnabled = true,
                AdvancedMotionDetectionHumanOnlyMode = false,
                EnableAudioRecording = true,
                EnableWhiteLeds = 0,
                LightSettings = new LightSettings { Brightness = 80 }
            });

            Assert.NotNull(f);
            Assert.True(f!.EnableVod);
            Assert.Equal(3, f.ExposureControl);
            Assert.Equal("custom", f.MotionSnoozePresetProfile);
            Assert.Equal("high", f.LiveViewPresetProfile);
            Assert.Equal(7, f.PirSensitivity1);
            Assert.True(f.VodSuspended);
            Assert.Equal(5, f.DoorbellVolume);
            Assert.Equal("active", f.VodStatus);
            Assert.True(f.AdvancedMotionDetectionEnabled);
            Assert.False(f.AdvancedMotionDetectionHumanOnlyMode);
            Assert.True(f.EnableAudioRecording);
            Assert.False(f.EnableWhiteLeds);
            Assert.Equal(80, f.LightBrightness);
        }

        [Fact]
        public void ToDeviceFields_StickupCamFlagOutsideZeroOne_MapsToNull()
        {
            DeviceDetailFields? f = StickupFields(new StickupCamSettings { VodSuspended = 2, EnableWhiteLeds = 9 });

            Assert.NotNull(f);
            Assert.Null(f!.VodSuspended);
            Assert.Null(f.EnableWhiteLeds);
        }

        [Fact]
        public void ToDeviceFields_StickupCamWithoutSettings_LeavesSettingsScalarsNull()
        {
            DeviceDetailFields? f = StickupFields(null);

            Assert.NotNull(f);
            Assert.Null(f!.EnableVod);
            Assert.Null(f.ExposureControl);
            Assert.Null(f.VodStatus);
            Assert.Null(f.LightBrightness);
            Assert.Null(f.MotionSnoozePresets);
            Assert.Null(f.LiveViewPresets);
            Assert.Null(f.AdvancedMotionZones);
        }

        [Fact]
        public void ToDeviceFields_Owner_MapsIdNameAndEmailAsText()
        {
            DeviceDetailFields? f = StickupFields(null, new Owner { Id = 42, FirstName = "Pat", LastName = "Lee", Email = "pat@example.com" });

            Assert.NotNull(f);
            Assert.Equal("42", f!.OwnerId);
            Assert.Equal("Pat", f.OwnerFirstName);
            Assert.Equal("Lee", f.OwnerLastName);
            Assert.Equal("pat@example.com", f.OwnerEmail);
        }

        [Fact]
        public void ToDeviceFields_Chime_MapsVolumeAudioIdsDoNotDisturbAndOwner()
        {
            var devices = new Devices
            {
                Chimes = [new Chime
                {
                    Id = 789,
                    Settings = new ChimeSettings { Volume = 4, DingAudioUserId = "u1", DingAudioId = "d1", MotionAudioUserId = "u2", MotionAudioId = "m1" },
                    DoNotDisturb = new DoNotDisturb { SecondsLeft = 1800.0m },
                    Owner = new Owner { Id = 7 }
                }]
            };

            DeviceDetailFields? f = RingDeviceDetailsMapper.Find(devices, "789")?.DeviceFields;

            Assert.NotNull(f);
            Assert.Equal(4, f!.ChimeVolume);
            Assert.Equal("u1", f.DingAudioUserId);
            Assert.Equal("d1", f.DingAudioId);
            Assert.Equal("u2", f.MotionAudioUserId);
            Assert.Equal("m1", f.MotionAudioId);
            Assert.Equal(1800, f.DoNotDisturbSecondsLeft);
            Assert.Equal("7", f.OwnerId);
            Assert.Null(f.OwnerEmail);
        }

        [Fact]
        public void ToDeviceFields_ChimeWithoutSettingsOrDoNotDisturb_LeavesThoseNull()
        {
            var devices = new Devices { Chimes = [new Chime { Id = 789 }] };

            DeviceDetailFields? f = RingDeviceDetailsMapper.Find(devices, "789")?.DeviceFields;

            Assert.NotNull(f);
            Assert.Null(f!.ChimeVolume);
            Assert.Null(f.DingAudioId);
            Assert.Null(f.DoNotDisturbSecondsLeft);
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_MapsPresetsInRingOrder()
        {
            DeviceDetailFields? f = StickupFields(new StickupCamSettings
            {
                MotionSnoozePresets = ["1 hour", "until sunrise"],
                LiveViewPresets = ["night"]
            });

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildren(f!, DeviceId);

            Assert.NotNull(children);
            Assert.Equal(2, children!.MotionSnoozePresets.Count);
            Assert.Equal(DeviceId, children.MotionSnoozePresets[0].DeviceId);
            Assert.Equal(0, children.MotionSnoozePresets[0].Position);
            Assert.Equal("1 hour", children.MotionSnoozePresets[0].Preset);
            Assert.Equal(1, children.MotionSnoozePresets[1].Position);
            Assert.Equal("until sunrise", children.MotionSnoozePresets[1].Preset);
            _ = Assert.Single(children.LiveViewPresets);
            Assert.Equal("night", children.LiveViewPresets[0].Preset);
        }

        [Fact]
        public void ToSettingsChildren_StickupCam_MapsAdvancedZonesAndSkipsAbsentVertices()
        {
            DeviceDetailFields? f = StickupFields(new StickupCamSettings
            {
                AdvancedMotionZones = new AdvancedMotionZones
                {
                    Zone1 = new Zone
                    {
                        Name = "driveway",
                        State = 1,
                        Vertex1 = new Vertex { X = 0.1, Y = 0.2 },
                        Vertex2 = new Vertex { X = 0.3, Y = 0.4 }
                    }
                }
            });

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildren(f!, DeviceId);

            Assert.NotNull(children);
            DeviceAdvancedMotionZone zone = Assert.Single(children!.AdvancedMotionZones);
            Assert.Equal("zone1", zone.ZoneKey);
            Assert.Equal("driveway", zone.Name);
            Assert.Equal(1, zone.State);
            Assert.Equal(2, zone.Vertices.Count);
            Assert.Equal(1, zone.Vertices[0].Position);
            Assert.Equal(0.1, zone.Vertices[0].X);
            Assert.Equal(0.4, zone.Vertices[1].Y);
        }

        [Fact]
        public void ToSettingsChildren_NullFields_ReturnsNull()
        {
            Assert.Null(RingDeviceDetailsMapper.ToSettingsChildren(null, DeviceId));
        }

        [Fact]
        public void ToSettingsChildren_NoPresetsOrZones_ReturnsEmptyLists()
        {
            var f = new DeviceDetailFields(EnableVod: true);

            DeviceSettingsChildren? children = RingDeviceDetailsMapper.ToSettingsChildren(f, DeviceId);

            Assert.NotNull(children);
            Assert.Empty(children!.MotionSnoozePresets);
            Assert.Empty(children.LiveViewPresets);
            Assert.Empty(children.AdvancedMotionZones);
        }

        [Fact]
        public void ToFeatures_StickupCamShowVodSettings_MapsToFeatureRow()
        {
            var devices = new Devices { StickupCams = [new StickupCam { Id = 456L, Features = new StickupCamFeatures { ShowVodSettings = true } }] };
            RingDeviceDetails details = RingDeviceDetailsMapper.Find(devices, "456")!;

            DeviceFeatures? features = RingDeviceDetailsMapper.ToFeatures(details, DeviceId);

            Assert.NotNull(features);
            Assert.True(features!.ShowVodSettings);
        }

        [Fact]
        public void Find_ChimeWithFeatures_MapsRingtonesEnabledToFeatureRow()
        {
            var devices = new Devices { Chimes = [new Chime { Id = 789, Features = new ChimeFeatures { RingtonesEnabled = true } }] };
            RingDeviceDetails details = RingDeviceDetailsMapper.Find(devices, "789")!;

            DeviceFeatures? features = RingDeviceDetailsMapper.ToFeatures(details, DeviceId);

            Assert.True(details.HasFeatures);
            Assert.NotNull(features);
            Assert.True(features!.RingtonesEnabled);
        }
    }
}
