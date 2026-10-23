using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    /// <summary>DeviceDto carries the unmodeled settings JSON in both directions.</summary>
    public class DeviceDtoSettingsMetadataTests
    {
        [Fact]
        public void DeviceDto_ToDto_CopiesSettingsMetadataJson()
        {
            var device = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Front", Type = "doorbot", SettingsMetadataJson = "{\"live_view_disabled\":false}" };

            DeviceDto dto = device.ToDto();

            Assert.Equal("{\"live_view_disabled\":false}", dto.SettingsMetadataJson);
        }

        [Fact]
        public void DeviceDto_ToDomain_CopiesSettingsMetadataJson()
        {
            var dto = new DeviceDto(Guid.NewGuid(), Guid.NewGuid(), "1", "Front", "doorbot", null, null, null, null, null, null, SyncStatusDto.Pending, null, SettingsMetadataJson: "{\"live_view_disabled\":false}");

            Device device = dto.ToDomain();

            Assert.Equal("{\"live_view_disabled\":false}", device.SettingsMetadataJson);
        }
    }
}
