using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Core.Services;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class BulkValidationServiceTests
    {
        private readonly Mock<ILogger<BulkValidationService>> _loggerMock;
        private readonly Mock<IEvidenceValidationService> _validationServiceMock;
        private readonly Mock<IDeviceRepository> _deviceRepositoryMock;
        private readonly BulkValidationService _service;

        public BulkValidationServiceTests()
        {
            _loggerMock = new Mock<ILogger<BulkValidationService>>();
            _validationServiceMock = new Mock<IEvidenceValidationService>();
            _deviceRepositoryMock = new Mock<IDeviceRepository>();
            _service = new BulkValidationService(
                _validationServiceMock.Object,
                _deviceRepositoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task RunFullValidationAsync_NoDevices_ReturnsZeroValidated()
        {
            var devices = new List<Device>();
            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(0, result.DevicesValidated);
            Assert.Equal(0, result.TotalDiscrepancies);
            Assert.Equal(0, result.FailedDevices);
        }

        [Fact]
        public async Task RunFullValidationAsync_DeviceWithoutProviderDeviceId_SkipsDevice()
        {
            var devices = new List<Device>
            {
                new Device { Id = Guid.NewGuid(), Name = "Camera1", ProviderDeviceId = null, Type = "camera" },
                new Device { Id = Guid.NewGuid(), Name = "Camera2", ProviderDeviceId = "", Type = "camera" }
            };
            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(0, result.DevicesValidated);
            Assert.Equal(0, result.FailedDevices);
            _validationServiceMock.Verify(
                s => s.ReconcileWithProviderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RunFullValidationAsync_SingleDeviceWithoutDiscrepancies_SuccessfulValidation()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "TestCamera", ProviderDeviceId = "ring-123", Type = "camera" };
            var devices = new List<Device> { device };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(deviceId, "ring-123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);
            Assert.Equal(0, result.TotalDiscrepancies);
            Assert.Equal(0, result.FailedDevices);
            _validationServiceMock.Verify(
                s => s.ReconcileWithProviderAsync(deviceId, "ring-123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RunFullValidationAsync_MultipleDevices_ValidatesAll()
        {
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var devices = new List<Device>
            {
                new Device { Id = device1Id, Name = "Camera1", ProviderDeviceId = "ring-1", Type = "camera" },
                new Device { Id = device2Id, Name = "Camera2", ProviderDeviceId = "ring-2", Type = "camera" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(device1Id, "ring-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(device2Id, "ring-2", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.DevicesValidated);
            Assert.Equal(0, result.FailedDevices);
        }

        [Fact]
        public async Task RunFullValidationAsync_WithDiscrepancies_CountsNewEventsAndMetadataChanges()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "TestCamera", ProviderDeviceId = "ring-123", Type = "camera" };
            var devices = new List<Device> { device };

            var discrepancies = new List<ReconciliationDiscrepancy>
            {
                new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event1" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event2" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.MetadataChanged, ProviderEventId = "event3" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(deviceId, "ring-123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(discrepancies);

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);
            Assert.Equal(3, result.TotalDiscrepancies);
            Assert.Equal(2, result.NewEventsInserted);
            Assert.Equal(1, result.MetadataUpdated);
            Assert.Equal(0, result.FailedDevices);
        }

        [Fact]
        public async Task RunFullValidationAsync_DeviceThrowsException_RecordsFailure()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "FailingCamera", ProviderDeviceId = "ring-bad", Type = "camera" };
            var devices = new List<Device> { device };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(deviceId, "ring-bad", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("API connection failed"));

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(0, result.DevicesValidated);
            Assert.Equal(1, result.FailedDevices);
            Assert.Single(result.ErrorsByDevice);
            Assert.Contains("API connection failed", result.ErrorsByDevice[0]);
        }

        [Fact]
        public async Task RunFullValidationAsync_MixedDevices_ValidatesAllWithErrors()
        {
            var successDeviceId = Guid.NewGuid();
            var failDeviceId = Guid.NewGuid();
            var skipDeviceId = Guid.NewGuid();

            var devices = new List<Device>
            {
                new Device { Id = successDeviceId, Name = "GoodCamera", ProviderDeviceId = "ring-good", Type = "camera" },
                new Device { Id = failDeviceId, Name = "FailCamera", ProviderDeviceId = "ring-fail", Type = "camera" },
                new Device { Id = skipDeviceId, Name = "SkipCamera", ProviderDeviceId = null, Type = "camera" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(successDeviceId, "ring-good", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(failDeviceId, "ring-fail", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network timeout"));

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);
            Assert.Equal(1, result.FailedDevices);
            Assert.Single(result.ErrorsByDevice);
        }

        [Fact]
        public async Task RunFullValidationAsync_UsesCustomDateRange_WhenOverridesProvided()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "TestCamera", ProviderDeviceId = "ring-123", Type = "camera" };
            var devices = new List<Device> { device };

            var customStart = DateTime.Parse("2024-01-01");
            var customEnd = DateTime.Parse("2024-01-31");

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(deviceId, "ring-123", customStart, customEnd, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            var result = await _service.RunFullValidationAsync(customStart, customEnd);

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);
            _validationServiceMock.Verify(
                s => s.ReconcileWithProviderAsync(deviceId, "ring-123", customStart, customEnd, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RunFullValidationAsync_UsesDefault90DayRange_WhenNoOverride()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "TestCamera", ProviderDeviceId = "ring-123", Type = "camera" };
            var devices = new List<Device> { device };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);

            // Verify the captured arguments were roughly 90 days apart
            _validationServiceMock.Verify(
                s => s.ReconcileWithProviderAsync(
                    deviceId,
                    "ring-123",
                    It.Is<DateTime>(d => d.Date == DateTime.Today.AddDays(-90)),
                    It.Is<DateTime>(d => d.Date == DateTime.Today),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RunFullValidationAsync_CalculatesElapsedTime()
        {
            var devices = new List<Device>
            {
                new Device { Id = Guid.NewGuid(), Name = "Camera1", ProviderDeviceId = "ring-1", Type = "camera" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>());

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.True(result.ElapsedTime.TotalMilliseconds >= 0);
            Assert.NotEqual(default, result.RanAtUtc);
        }

        [Fact]
        public async Task RunFullValidationAsync_AcceptsCancellationToken()
        {
            var devices = new List<Device>();
            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            var ct = CancellationToken.None;
            var result = await _service.RunFullValidationAsync(ct: ct);

            Assert.NotNull(result);
            _deviceRepositoryMock.Verify(r => r.ListAsync(ct), Times.Once);
        }

        [Fact]
        public async Task RunFullValidationAsync_CountsMultipleDiscrepancyTypesCorrectly()
        {
            var deviceId = Guid.NewGuid();
            var device = new Device { Id = deviceId, Name = "TestCamera", ProviderDeviceId = "ring-123", Type = "camera" };
            var devices = new List<Device> { device };

            var discrepancies = new List<ReconciliationDiscrepancy>
            {
                new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event1" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event2" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event3" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.MetadataChanged, ProviderEventId = "event4" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.MetadataChanged, ProviderEventId = "event5" },
                new ReconciliationDiscrepancy { Type = DiscrepancyType.MissingFromProvider, ProviderEventId = "event6" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(deviceId, "ring-123", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(discrepancies);

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(1, result.DevicesValidated);
            Assert.Equal(6, result.TotalDiscrepancies);
            Assert.Equal(3, result.NewEventsInserted);
            Assert.Equal(2, result.MetadataUpdated);
        }

        [Fact]
        public async Task RunFullValidationAsync_MultipleDevicesWithMixedResults_AggregatesAll()
        {
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var device3Id = Guid.NewGuid();

            var devices = new List<Device>
            {
                new Device { Id = device1Id, Name = "Camera1", ProviderDeviceId = "ring-1", Type = "camera" },
                new Device { Id = device2Id, Name = "Camera2", ProviderDeviceId = "ring-2", Type = "camera" },
                new Device { Id = device3Id, Name = "Camera3", ProviderDeviceId = "ring-3", Type = "camera" }
            };

            _deviceRepositoryMock
                .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(devices);

            // Device 1: 2 discrepancies
            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(device1Id, "ring-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>
                {
                    new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event1" },
                    new ReconciliationDiscrepancy { Type = DiscrepancyType.MetadataChanged, ProviderEventId = "event2" }
                });

            // Device 2: 1 discrepancy
            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(device2Id, "ring-2", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconciliationDiscrepancy>
                {
                    new ReconciliationDiscrepancy { Type = DiscrepancyType.NewEventFoundOnProvider, ProviderEventId = "event3" }
                });

            // Device 3: throws exception
            _validationServiceMock
                .Setup(s => s.ReconcileWithProviderAsync(device3Id, "ring-3", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Device offline"));

            var result = await _service.RunFullValidationAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.DevicesValidated);
            Assert.Equal(1, result.FailedDevices);
            Assert.Equal(3, result.TotalDiscrepancies);
            Assert.Equal(2, result.NewEventsInserted);
            Assert.Equal(1, result.MetadataUpdated);
            Assert.Single(result.ErrorsByDevice);
        }
    }
}
