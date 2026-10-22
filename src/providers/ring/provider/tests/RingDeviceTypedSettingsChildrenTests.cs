using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Typed settings details map to child rows; a provider that does not supply them leaves those rows untouched.</summary>
    public class RingDeviceTypedSettingsChildrenTests
    {
        private static readonly Guid DeviceId = Guid.Parse("66666666-7777-8888-9999-000000000000");

        [Fact]
        public void RingDeviceDetailsMapper_ToSettingsChildren_TypedDetails_BecomeRowsForDevice()
        {
            var fields = new DeviceDetailFields(
                MotionSnoozePresets: [],
                LiveViewPresets: [],
                AdvancedMotionZones: [],
                OfflineMotion: new OfflineMotionDetail(true, false, 5000, 360, 2, 30),
                DetectionTypes: [new DetectionTypeDetail("human", true, "cloud", false, true)],
                DetectionThresholds: [new DetectionThresholdDetail("loitering", 10.0)]);

            var children = RingDeviceDetailsMapper.ToSettingsChildren(fields, DeviceId)!;

            var offline = Assert.Single(children.OfflineMotion!);
            Assert.Equal(DeviceId, offline.DeviceId);
            Assert.Equal(5000, offline.MaxUploadKb);
            Assert.Equal(30, offline.PeriodAfterSecs);
            var human = Assert.Single(children.DetectionTypes!);
            Assert.Equal(DeviceId, human.DeviceId);
            Assert.Equal("human", human.DetectionType);
            Assert.Equal("cloud", human.Mode);
            Assert.Equal(true, human.Notification);
            var threshold = Assert.Single(children.DetectionThresholds!);
            Assert.Equal("loitering", threshold.DetectionType);
            Assert.Equal(10.0, threshold.Threshold);
        }

        [Fact]
        public void RingDeviceDetailsMapper_ToSettingsChildren_NoOfflineMotionSent_ClearsOfflineRows()
        {
            var fields = new DeviceDetailFields(
                MotionSnoozePresets: [],
                LiveViewPresets: [],
                AdvancedMotionZones: [],
                OfflineMotion: null,
                DetectionTypes: []);

            var children = RingDeviceDetailsMapper.ToSettingsChildren(fields, DeviceId)!;

            Assert.Empty(children.OfflineMotion!);
            Assert.Empty(children.DetectionTypes!);
        }

        [Fact]
        public void RingDeviceDetailsMapper_ToSettingsChildren_ChimeWithoutTypedDetails_LeavesRowsUntouched()
        {
            var fields = new DeviceDetailFields(
                MotionSnoozePresets: [],
                LiveViewPresets: [],
                AdvancedMotionZones: []);

            var children = RingDeviceDetailsMapper.ToSettingsChildren(fields, DeviceId)!;

            Assert.Null(children.OfflineMotion);
            Assert.Null(children.DetectionTypes);
            Assert.Null(children.DetectionThresholds);
        }
    }
}
