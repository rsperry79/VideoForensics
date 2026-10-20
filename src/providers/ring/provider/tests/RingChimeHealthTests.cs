using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Chimes, including the Chime Pro Wi-Fi bridge, report health through the same ring_devices payload as cameras.</summary>
    public class RingChimeHealthTests
    {
        private static async Task<IReadOnlyList<DeviceHealthReading>> FetchAsync(Devices devices)
        {
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(devices);
            _ = sessionProvider.Setup(sp => sp.GetSession()).Returns(session.Object);

            var service = new RingHealthSource(new Mock<ILogger<RingHealthSource>>().Object, sessionProvider.Object);
            return await service.FetchHealthAsync(CancellationToken.None);
        }

        [Fact]
        public async Task FetchHealthAsync_ChimeProWithHealth_ReturnsReadingWithWifiFields()
        {
            var devices = new Devices
            {
                Chimes =
                [
                    new Chime
                    {
                        Id = 302,
                        Kind = "chime_pro_v2",
                        Health = new DeviceHealth
                        {
                            Connected = true,
                            Rssi = -48,
                            WifiName = "Ring-Bridge",
                            WifiIsRingNetwork = true,
                            NetworkConnectionValue = "wifi",
                            ExtPowerState = 1,
                        },
                    },
                ],
            };

            IReadOnlyList<DeviceHealthReading> readings = await FetchAsync(devices);

            DeviceHealthReading reading = Assert.Single(readings);
            Assert.Equal("302", reading.ProviderDeviceId);
            Assert.Equal(true, reading.Connected);
            Assert.Equal(-48, reading.Rssi);
            Assert.Equal("Ring-Bridge", reading.WifiName);
            Assert.Equal(true, reading.WifiIsRingNetwork);
            Assert.Equal("wifi", reading.NetworkConnectionValue);
        }

        [Fact]
        public async Task FetchHealthAsync_ChimeWithoutHealth_ReturnsNoReading()
        {
            var devices = new Devices
            {
                Chimes = [new Chime { Id = 303, Kind = "chime_v2", Health = null }],
            };

            IReadOnlyList<DeviceHealthReading> readings = await FetchAsync(devices);

            Assert.Empty(readings);
        }
    }
}
