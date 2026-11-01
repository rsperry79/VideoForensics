using System.Text.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>The download path rebuilds settings children from the device list, which is partial. Rows the list does not carry must stay untouched.</summary>
    public class RingDownloadSettingsChildrenTests
    {
        private static readonly Guid DeviceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        [Fact]
        public void RingDeviceDetailsMapper_ToDeviceListSettingsChildren_ListWithoutAdvancedMotionSettings_LeavesThoseRowsNull()
        {
            // A device-list entry with a settings object but no advanced_motion_settings, video_settings or advanced zones.
            Doorbot doorbot = JsonSerializer.Deserialize<Doorbot>("{\"id\":1,\"settings\":{\"doorbell_volume\":2}}")!;
            RingDeviceDetails details = RingDeviceDetailsMapper.Find(new Devices { Doorbots = [doorbot] }, "1")!;

            DeviceSettingsChildren children = RingDeviceDetailsMapper.ToDeviceListSettingsChildren(details, DeviceId)!;

            Assert.Null(children.AdvancedMotionZones);
            Assert.Null(children.RingSettingGroups);
            Assert.Null(children.RingSettings);
            Assert.Null(children.GeneralSettings);
        }    }
}
