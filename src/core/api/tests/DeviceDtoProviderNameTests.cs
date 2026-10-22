using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Core.Api.Tests
{
    /// <summary>DeviceDto carries the linked provider name in both directions.</summary>
    public class DeviceDtoProviderNameTests
    {
        [Fact]
        public void DeviceDto_ToDto_CopiesProviderName()
        {
            var device = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Front", Type = "doorbot", ProviderName = "Ring" };

            DeviceDto dto = device.ToDto();

            Assert.Equal("Ring", dto.ProviderName);
        }

        [Fact]
        public void DeviceDto_ToDomain_CopiesProviderName()
        {
            var dto = new DeviceDto(Guid.NewGuid(), Guid.NewGuid(), "1", "Front", "doorbot", null, null, null, null, null, null, SyncStatusDto.Pending, null, ProviderName: "Wyze");

            Device device = dto.ToDomain();

            Assert.Equal("Wyze", device.ProviderName);
        }

        [Fact]
        public void DeviceDto_ToDto_NoLinkedProvider_ProviderNameNull()
        {
            var device = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Front", Type = "doorbot" };

            Assert.Null(device.ToDto().ProviderName);
        }
    }
}
