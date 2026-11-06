using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

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

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_UnknownRingKind_EnsuresDiscoveredModel()
        {
            // Arrange: a kind the catalog does not list must be recorded so the database knows about it.
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Front", Type = "doorbot" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(RingKind: "doorbell_brand_new"));

            // Act
            _ = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            // Assert
            data.Verify(d => d.EnsureDeviceModelAsync("Ring", "doorbell_brand_new", "Doorbell", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_UnknownChimeKind_UsesChimeFamily()
        {
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Hall", Type = "chime" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(RingKind: "chime_brand_new"));

            _ = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync("Ring", "chime_brand_new", "Chime", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_KnownRingKind_DoesNotEnsureModel()
        {
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Hall", Type = "chime" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(RingKind: "chime_pro_v2"));

            _ = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_NoRingKind_DoesNotEnsureModel()
        {
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Front", Type = "doorbot" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(ChimeVolume: 2));

            _ = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            data.Verify(d => d.EnsureDeviceModelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RingDeviceSettingsPersister_PersistAsync_FeatureDetails_PassesScalarsAndFeatureStatesToWriter()
        {
            // Arrange: the details carry a features graph; both the scalar row and the state rows must reach the writer in the same call.
            Mock<IVideoForensicsDataClient> data = CreateDataClient();
            Mock<IDeviceRepository> devices = CreateDeviceRepository(new[] { new Device { Id = DeviceGuid, ProviderDeviceId = ProviderDeviceId, Name = "Front", Type = "doorbell" } });
            var persister = new RingDeviceSettingsPersister(data.Object, devices.Object, Mock.Of<ILogger>());
            RingDeviceDetails details = CreateDetails(new DeviceDetailFields(ChimeVolume: 2)) with
            {
                HasFeatures = true,
                FeatureDetails = new RingFeatureDetails(
                    new DeviceFeatures { CfesEligible = true },
                    [new RingDeviceFeatureState { FeatureKey = "smart_video_search", Enabled = false }])
            };

            // Act
            _ = await persister.PersistAsync(ProviderDeviceId, details, CancellationToken.None);

            // Assert
            data.Verify(d => d.RecordDeviceDetailsAsync(
                DeviceGuid, null, null,
                It.Is<DeviceFeatures?>(f => f != null && f.CfesEligible == true),
                It.IsAny<IReadOnlyList<DeviceAlerts>?>(),
                It.IsAny<CancellationToken>(),
                It.Is<DeviceSettingsChildren?>(c => c != null && c.FeatureStates != null && c.FeatureStates.Count == 1
                    && c.FeatureStates[0].FeatureKey == "smart_video_search" && c.FeatureStates[0].DeviceId == DeviceGuid)), Times.Once);
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
