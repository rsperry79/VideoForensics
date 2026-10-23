using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Covers the health fields and chime health the earlier audit left unstored (ring_devices health id, RPC commands, chime health).</summary>
    public class RingDeviceHealthRemainingFieldsTests
    {
        private static Mock<Session> SessionWith(Devices devices)
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(devices);
            return session;
        }

        [Fact]
        public async Task CaptureDeviceHealthSnapshotAsync_RingHealthIdPowerVoltageOtaAndRpcCommands_ArePersisted()
        {
            var health = new DeviceHealth
            {
                Id = 77,
                BatteryVoltage = 12.3m,
                ExtPowerState = 1,
                OtaStatus = "up_to_date",
                SupportedRpcCommands = ["reboot", "siren"]
            };
            var dataClient = new Mock<IVideoForensicsDataClient>();
            VideoForensics.Data.Common.Entities.DeviceHealth? captured = null;
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<VideoForensics.Data.Common.Entities.DeviceHealth>(), It.IsAny<CancellationToken>()))
                .Callback<VideoForensics.Data.Common.Entities.DeviceHealth, CancellationToken>((h, _) => captured = h)
                .ReturnsAsync((VideoForensics.Data.Common.Entities.DeviceHealth h, CancellationToken _) => h);
            var capture = new RingDeviceHealthCapture(dataClient.Object, new Mock<ILogger>().Object);

            await capture.CaptureDeviceHealthSnapshotAsync(
                SessionWith(new Devices { Doorbots = [new Doorbot { Id = 100, Health = health }] }).Object, "100", Guid.NewGuid(), CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(77L, captured!.RingHealthId);
            Assert.Equal(12.3m, captured.BatteryVoltageValue);
            Assert.True(captured.IsExternalPowerConnected);
            Assert.Equal("up_to_date", captured.OtaStatus);
            Assert.Equal(["reboot", "siren"], captured.SupportedRpcCommands.Select(c => c.Command));
            Assert.Equal([0, 1], captured.SupportedRpcCommands.Select(c => c.Position));
        }

        [Fact]
        public async Task CaptureDeviceHealthSnapshotAsync_ChimeHealth_IsPersisted()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            VideoForensics.Data.Common.Entities.DeviceHealth? captured = null;
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<VideoForensics.Data.Common.Entities.DeviceHealth>(), It.IsAny<CancellationToken>()))
                .Callback<VideoForensics.Data.Common.Entities.DeviceHealth, CancellationToken>((h, _) => captured = h)
                .ReturnsAsync((VideoForensics.Data.Common.Entities.DeviceHealth h, CancellationToken _) => h);
            var capture = new RingDeviceHealthCapture(dataClient.Object, new Mock<ILogger>().Object);

            await capture.CaptureDeviceHealthSnapshotAsync(
                SessionWith(new Devices { Chimes = [new Chime { Id = 555, Health = new DeviceHealth { Connected = true, Id = 9 } }] }).Object,
                "555", Guid.NewGuid(), CancellationToken.None);

            Assert.NotNull(captured);
            Assert.Equal(9L, captured!.RingHealthId);
            Assert.True(captured.IsOnline);
        }

        [Fact]
        public void DeviceHealthMatcher_ChimeWithHealth_ReturnsHealth()
        {
            Entities.DeviceHealth? found = DeviceHealthMatcher.FindDeviceHealth(
                new Devices { Chimes = [new Chime { Id = 555, Health = new DeviceHealth { Connected = true } }] }, "555");

            Assert.NotNull(found);
            Assert.True(found!.Connected);
        }

        [Fact]
        public async Task FetchHealthAsync_ChimeHealthWithIdAndRpcCommands_ProducesReadingWithThem()
        {
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(new Devices
            {
                Chimes = [new Chime { Id = 555, Health = new DeviceHealth { Connected = true, Id = 9, SupportedRpcCommands = ["reboot"] } }]
            });
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);
            var service = new RingHealthSource(new Mock<ILogger<RingHealthSource>>().Object, sessionProvider.Object);

            IReadOnlyList<DeviceHealthReading> readings = await service.FetchHealthAsync(CancellationToken.None);

            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("555", reading.ProviderDeviceId);
            Assert.Equal(9L, reading.RingHealthId);
            Assert.Equal(["reboot"], reading.SupportedRpcCommands);
        }
    }
}
