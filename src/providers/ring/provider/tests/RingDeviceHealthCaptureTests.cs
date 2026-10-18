using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Tests that RingDeviceHealthCapture writes Ring's health detail fields onto the DeviceHealths row it records.</summary>
    public class RingDeviceHealthCaptureTests
    {
        [Fact]
        public async Task CaptureDeviceHealthSnapshotAsync_HealthDetailFields_ArePersisted()
        {
            // Arrange
            var health = new DeviceHealth
            {
                Connected = true,
                DeviceType = "doorbell_v4",
                PacketLoss = 0.5,
                PacketLossCategory = "low",
                Firmware = "2.0.1",
                UpdatedAt = "2026-10-16T12:30:00Z",
                BatteryPresent = true,
                WifiIsRingNetwork = false,
                PtzConnected = true
            };
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(new Devices { Doorbots = [new Doorbot { Id = 100, Health = health }] });

            VideoForensics.Data.Common.Entities.DeviceHealth? captured = null;
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<VideoForensics.Data.Common.Entities.DeviceHealth>(), It.IsAny<CancellationToken>()))
                .Callback<VideoForensics.Data.Common.Entities.DeviceHealth, CancellationToken>((h, _) => captured = h)
                .ReturnsAsync((VideoForensics.Data.Common.Entities.DeviceHealth h, CancellationToken _) => h);
            var capture = new RingDeviceHealthCapture(dataClient.Object, new Mock<ILogger>().Object);

            // Act
            await capture.CaptureDeviceHealthSnapshotAsync(session.Object, "100", Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal("doorbell_v4", captured!.DeviceType);
            Assert.Equal(0.5, captured.PacketLoss);
            Assert.Equal("low", captured.PacketLossCategory);
            Assert.Equal("2.0.1", captured.Firmware);
            Assert.Equal(new DateTime(2026, 10, 16, 12, 30, 0, DateTimeKind.Utc), captured.UpdatedAtUtc);
            Assert.True(captured.BatteryPresent);
            Assert.False(captured.WifiIsRingNetwork);
            Assert.True(captured.PtzConnected);
        }

        [Fact]
        public async Task CaptureDeviceHealthSnapshotAsync_MissingHealthDetailFields_StayNull()
        {
            // Arrange
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices())
                .ReturnsAsync(new Devices { Doorbots = [new Doorbot { Id = 100, Health = new DeviceHealth { Connected = true } }] });

            VideoForensics.Data.Common.Entities.DeviceHealth? captured = null;
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<VideoForensics.Data.Common.Entities.DeviceHealth>(), It.IsAny<CancellationToken>()))
                .Callback<VideoForensics.Data.Common.Entities.DeviceHealth, CancellationToken>((h, _) => captured = h)
                .ReturnsAsync((VideoForensics.Data.Common.Entities.DeviceHealth h, CancellationToken _) => h);
            var capture = new RingDeviceHealthCapture(dataClient.Object, new Mock<ILogger>().Object);

            // Act
            await capture.CaptureDeviceHealthSnapshotAsync(session.Object, "100", Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Null(captured!.DeviceType);
            Assert.Null(captured.PacketLoss);
            Assert.Null(captured.BatteryPresent);
            Assert.Null(captured.UpdatedAtUtc);
            Assert.Null(captured.PtzConnected);
        }
    }
}
