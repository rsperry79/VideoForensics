using Moq;
using VideoForensics.Providers.Common.Contracts;
using Xunit;

namespace VideoForensics.Providers.Common.Tests
{
    public class ContractsTests
    {
        [Fact]
        public void AuthResult_CanBeCreatedSuccessfully()
        {
            // Arrange & Act
            var result = new AuthResult(
                Success: true,
                AuthToken: "test-token",
                ExpiresAt: DateTime.UtcNow.AddHours(1)
            );

            // Assert
            Assert.True(result.Success);
            Assert.Equal("test-token", result.AuthToken);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void AuthResult_CanBeCreatedWithError()
        {
            // Arrange & Act
            var result = new AuthResult(
                Success: false,
                ErrorMessage: "Invalid credentials"
            );

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid credentials", result.ErrorMessage);
            Assert.Null(result.AuthToken);
        }

        [Fact]
        public void Location_CanBeCreated()
        {
            // Arrange & Act
            var location = new Location(
                Id: "loc-123",
                Name: "Home",
                Address: "123 Main St"
            );

            // Assert
            Assert.Equal("loc-123", location.Id);
            Assert.Equal("Home", location.Name);
            Assert.Equal("123 Main St", location.Address);
        }

        [Fact]
        public void Device_CanBeCreated()
        {
            // Arrange & Act
            var device = new Device(
                Id: "dev-123",
                Name: "Front Door",
                Type: "doorbell",
                LocationId: "loc-123",
                IsOnline: true
            );

            // Assert
            Assert.Equal("dev-123", device.Id);
            Assert.Equal("Front Door", device.Name);
            Assert.Equal("doorbell", device.Type);
            Assert.True(device.IsOnline);
        }

        [Fact]
        public void DownloadResult_CanBeCreated()
        {
            // Arrange & Act
            var result = new DownloadResult(
                Success: true,
                FilesDownloaded: 5,
                BytesDownloaded: 1024000
            );

            // Assert
            Assert.True(result.Success);
            Assert.Equal(5, result.FilesDownloaded);
            Assert.Equal(1024000, result.BytesDownloaded);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void DeviceEvent_CanBeCreated()
        {
            // Arrange
            var timestamp = DateTime.UtcNow;

            // Act
            var @event = new DeviceEvent(
                Id: "evt-123",
                DeviceId: "dev-123",
                EventType: "motion",
                Timestamp: timestamp
            );

            // Assert
            Assert.Equal("evt-123", @event.Id);
            Assert.Equal("dev-123", @event.DeviceId);
            Assert.Equal("motion", @event.EventType);
            Assert.Equal(timestamp, @event.Timestamp);
        }

        [Fact]
        public void DeviceConfig_CanBeCreated()
        {
            // Arrange & Act
            var config = new DeviceConfig(
                DeviceId: "dev-123",
                MotionDetectionEnabled: true,
                MotionSensitivity: 75,
                RecordingMode: "motion"
            );

            // Assert
            Assert.Equal("dev-123", config.DeviceId);
            Assert.True(config.MotionDetectionEnabled);
            Assert.Equal(75, config.MotionSensitivity);
            Assert.Equal("motion", config.RecordingMode);
        }
    }
}
