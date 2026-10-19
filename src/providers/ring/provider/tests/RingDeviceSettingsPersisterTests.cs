using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Providers.Ring.Tests
{
    public class RingDeviceSettingsPersisterTests
    {
        private const string ProviderDeviceId = "1001";
        private static readonly Guid DeviceGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000f001");

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_CallsScalarUpdate()
        {
            // Arrange: the device row exists and the settings pull carries scalar detail fields.
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Front", Type = "doorbell" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(ChimeVolume: 2));

            // Act
            bool written = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            // Assert
            Assert.True(written);
            data.Verify(d => d.UpdateDeviceDetailFieldsAsync(DeviceGuid, details.DeviceFields!, It.IsAny<CancellationToken>()), Times.Once);
            data.Verify(d => d.RecordDeviceDetailsAsync(DeviceGuid, null, null, It.IsAny<DeviceFeatures?>(), It.IsAny<IReadOnlyList<DeviceAlerts>?>(), It.IsAny<CancellationToken>(), It.IsAny<DeviceSettingsChildren?>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_DeviceMissing_ReturnsFalseAndSkipsScalarUpdate()
        {
            // Arrange: no device row for the provider id yet.
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(Array.Empty<Device>());
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(ChimeVolume: 2));

            // Act
            bool written = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            // Assert
            Assert.False(written);
            data.Verify(d => d.UpdateDeviceDetailFieldsAsync(It.IsAny<Guid>(), It.IsAny<DeviceDetailFields>(), It.IsAny<CancellationToken>()), Times.Never);
            data.Verify(d => d.RecordDeviceDetailsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<DeviceCapabilities?>(), It.IsAny<DeviceFeatures?>(), It.IsAny<IReadOnlyList<DeviceAlerts>?>(), It.IsAny<CancellationToken>(), It.IsAny<DeviceSettingsChildren?>()), Times.Never);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_NoDeviceFields_SkipsScalarUpdate()
        {
            // Arrange: the settings pull carries no scalar detail fields; child rows are still written.
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Front", Type = "doorbell" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(null);

            // Act
            bool written = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            // Assert
            Assert.True(written);
            data.Verify(d => d.UpdateDeviceDetailFieldsAsync(It.IsAny<Guid>(), It.IsAny<DeviceDetailFields>(), It.IsAny<CancellationToken>()), Times.Never);
            data.Verify(d => d.RecordDeviceDetailsAsync(DeviceGuid, null, null, It.IsAny<DeviceFeatures?>(), It.IsAny<IReadOnlyList<DeviceAlerts>?>(), It.IsAny<CancellationToken>(), It.IsAny<DeviceSettingsChildren?>()), Times.Once);
        }

        private static Mock<IVideoForensicsDataClient> CreateDataClient()
        {
            var data = new Mock<IVideoForensicsDataClient>();
            _ = data.Setup(d => d.RecordDeviceDetailsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<DeviceCapabilities?>(),
                    It.IsAny<DeviceFeatures?>(),
                    It.IsAny<IReadOnlyList<DeviceAlerts>?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<DeviceSettingsChildren?>()))
                .Returns(Task.CompletedTask);
            _ = data.Setup(d => d.UpdateDeviceDetailFieldsAsync(It.IsAny<Guid>(), It.IsAny<DeviceDetailFields>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return data;
        }

        private static Mock<IDeviceRepository> CreateDeviceRepository(IReadOnlyList<Device> rows)
        {
            var devices = new Mock<IDeviceRepository>();
            _ = devices.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
            return devices;
        }

        private static RingDeviceDetails CreateDetails(DeviceDetailFields? fields)
        {
            return new RingDeviceDetails(
                TimeZone: null,
                FirmwareVersion: null,
                HasAudio: null,
                MotionsEnabled: null,
                NightVisionEnabled: null,
                HasFeatures: false,
                ShowRecordings: null,
                AdvancedMotionEnabled: null,
                PeopleOnlyEnabled: null,
                ShadowCorrectionEnabled: null,
                MotionMessageEnabled: null,
                AlertConnection: null,
                DeviceFields: fields);
        }
    }
}
