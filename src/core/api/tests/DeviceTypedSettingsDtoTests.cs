using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Core.Api.Tests
{
    /// <summary>The typed settings DTO carries every stored row, including an absent offline row as null.</summary>
    public class DeviceTypedSettingsDtoTests
    {
        [Fact]
        public void DeviceTypedSettingsDto_ToDto_MapsOfflineTypesAndThresholds()
        {
            Guid deviceId = Guid.NewGuid();
            var settings = new DeviceTypedSettings(
                new DeviceOfflineMotionSettings { DeviceId = deviceId, Subscribed = true, MaxUploadKb = 5000, PeriodAfterSecs = 30 },
                [new DeviceDetectionType { DeviceId = deviceId, DetectionType = "human", Enabled = true, Mode = "cloud", Record = false, Notification = true }],
                [new DeviceDetectionThreshold { DeviceId = deviceId, DetectionType = "loitering", Threshold = 10.0 }]);

            DeviceTypedSettingsDto dto = settings.ToDto(deviceId);

            Assert.Equal(deviceId, dto.DeviceId);
            Assert.Equal(5000, dto.OfflineMotion!.MaxUploadKb);
            Assert.Equal(30, dto.OfflineMotion.PeriodAfterSecs);
            DeviceDetectionTypeDto human = Assert.Single(dto.DetectionTypes);
            Assert.Equal("cloud", human.Mode);
            Assert.Equal(true, human.Notification);
            DeviceDetectionThresholdDto threshold = Assert.Single(dto.Thresholds);
            Assert.Equal(10.0, threshold.Threshold);
        }

        [Fact]
        public void DeviceTypedSettingsDto_ToDto_NoOfflineRow_OfflineMotionNull()
        {
            var settings = new DeviceTypedSettings(null, [], []);

            DeviceTypedSettingsDto dto = settings.ToDto(Guid.NewGuid());

            Assert.Null(dto.OfflineMotion);
            Assert.Empty(dto.DetectionTypes);
            Assert.Empty(dto.Thresholds);
        }
    }
}
