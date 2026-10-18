namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Data.Common.Entities;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;
    using DeviceConfigEntity = VideoForensics.Data.Common.Entities.DeviceConfigSnapshot;

    /// <summary>
    /// Verifies that the device configuration fetched during a sync is persisted as a snapshot, so it is never discarded.
    /// </summary>
    public class EventPullServiceDeviceConfigTests
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _eventAndConfig = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IDeviceConfigRepository> _configs = new();

        private EventPullService CreateSut() => new(
            _eventAndConfig.Object,
            _devices.Object,
            _locations.Object,
            _accounts.Object,
            _discovery.Object,
            deviceConfigRepository: _configs.Object);

        private void SetUpAccountAndDevice(DeviceConfig fetched)
        {
            var cancellationToken = CancellationToken.None;
            var locationId = Guid.NewGuid();

            _accounts.Setup(r => r.GetAsync(_accountId, cancellationToken)).ReturnsAsync(new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            });

            var location = new LocationEntity { Id = locationId, ProviderLocationId = "location-123", Name = "Home" };
            var device = new DeviceEntity
            {
                Id = _deviceId,
                LocationId = locationId,
                ProviderDeviceId = "device-123",
                Name = "Front Door",
                Type = "doorbell"
            };

            DiscoveryStub.Wire(_accountId, location, new[] { device }, _discovery, _locations, _devices);

            _eventAndConfig.Setup(s => s.GetDeviceConfigAsync(_accountId, "device-123", cancellationToken))
                .ReturnsAsync(fetched);
        }

        private static DeviceConfig Fetched(bool motion = true, int sensitivity = 80, string mode = "always") =>
            new(DeviceId: "device-123", MotionDetectionEnabled: motion, MotionSensitivity: sensitivity, RecordingMode: mode);

        [Fact]
        public async Task PullAccountEventsAsync_ConfigFetchedWithNoPriorSnapshot_AppendsSnapshot()
        {
            var cancellationToken = CancellationToken.None;
            SetUpAccountAndDevice(Fetched());
            _configs.Setup(r => r.GetLatestAsync(_deviceId, cancellationToken)).ReturnsAsync((DeviceConfigEntity?)null);

            await CreateSut().PullAccountEventsAsync(_accountId, null, null, cancellationToken);

            _configs.Verify(r => r.AppendSnapshotAsync(
                It.Is<DeviceConfigEntity>(s =>
                    s.DeviceId == _deviceId &&
                    s.MotionDetectionEnabled == true &&
                    s.MotionSensitivity == "80" &&
                    s.RecordingMode == "always" &&
                    s.Source == DeviceConfigSource.Fetched),
                cancellationToken), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ConfigUnchangedSinceLatestSnapshot_DoesNotAppend()
        {
            var cancellationToken = CancellationToken.None;
            SetUpAccountAndDevice(Fetched());
            _configs.Setup(r => r.GetLatestAsync(_deviceId, cancellationToken)).ReturnsAsync(new DeviceConfigEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = _deviceId,
                MotionDetectionEnabled = true,
                MotionSensitivity = "80",
                RecordingMode = "always",
                CapturedAtUtc = DateTime.UtcNow.AddHours(-1),
                Source = DeviceConfigSource.Fetched
            });

            await CreateSut().PullAccountEventsAsync(_accountId, null, null, cancellationToken);

            _configs.Verify(r => r.AppendSnapshotAsync(It.IsAny<DeviceConfigEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ConfigChangedSinceLatestSnapshot_AppendsSnapshot()
        {
            var cancellationToken = CancellationToken.None;
            SetUpAccountAndDevice(Fetched(motion: false, sensitivity: 20, mode: "motion"));
            _configs.Setup(r => r.GetLatestAsync(_deviceId, cancellationToken)).ReturnsAsync(new DeviceConfigEntity
            {
                Id = Guid.NewGuid(),
                DeviceId = _deviceId,
                MotionDetectionEnabled = true,
                MotionSensitivity = "80",
                RecordingMode = "always",
                CapturedAtUtc = DateTime.UtcNow.AddHours(-1),
                Source = DeviceConfigSource.Fetched
            });

            await CreateSut().PullAccountEventsAsync(_accountId, null, null, cancellationToken);

            _configs.Verify(r => r.AppendSnapshotAsync(
                It.Is<DeviceConfigEntity>(s => s.MotionDetectionEnabled == false && s.MotionSensitivity == "20" && s.RecordingMode == "motion"),
                cancellationToken), Times.Once);
        }
    }
}
