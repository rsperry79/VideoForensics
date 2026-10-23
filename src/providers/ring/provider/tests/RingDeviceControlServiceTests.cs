using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using CommonContracts = VideoForensics.Core.Providers.Common.Contracts;

using DeviceEntity = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingDeviceControlService: each control resolves the device's Ring account from its location links
    /// and uses only that account's SDK session. Ambiguous or missing account links fail closed before any provider call.
    /// </summary>
    public class RingDeviceControlServiceTests
    {
        private const string DoorbotProviderId = "123";
        private const string DoorbotName = "Front Door";
        private const string ChimeProviderId = "456";

        private static readonly Guid LocationId = Guid.NewGuid();
        private static readonly Guid RingAccountId = Guid.NewGuid();
        private static readonly Guid OtherRingAccountId = Guid.NewGuid();
        private static readonly Guid WyzeAccountId = Guid.NewGuid();

        private sealed class Harness
        {
            public required CommonContracts.IDeviceControlService Service { get; init; }
            public required Mock<Session> Session { get; init; }
            public required Mock<ISessionProvider> SessionProvider { get; init; }
        }

        private static List<DeviceEntity> BuildDeviceRows()
        {
            return
            [
                new DeviceEntity { Id = Guid.NewGuid(), LocationId = LocationId, ProviderDeviceId = DoorbotProviderId, Name = DoorbotName, Type = "doorbell" },
                new DeviceEntity { Id = Guid.NewGuid(), LocationId = LocationId, ProviderDeviceId = ChimeProviderId, Name = "Hall Chime", Type = "chime" },
            ];
        }

        private static ProviderAccount BuildAccount(Guid id, string providerName)
        {
            return new ProviderAccount { Id = id, UserId = Guid.NewGuid(), ProviderName = providerName };
        }

        private static Harness Build(IReadOnlyList<ProviderAccount>? linkedAccounts = null, List<DeviceEntity>? deviceRows = null)
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(session.Object);

            var deviceRepository = new Mock<IDeviceRepository>();
            _ = deviceRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(deviceRows ?? BuildDeviceRows());

            var locationRepository = new Mock<ILocationRepository>();
            _ = locationRepository.Setup(r => r.ListProviderAccountsForLocationAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(linkedAccounts ?? [BuildAccount(RingAccountId, "Ring")]);

            ILogger logger = new Mock<ILogger>().Object;
            var service = new RingDeviceControlService(logger, sessionProvider.Object, deviceRepository.Object, locationRepository.Object);
            return new Harness { Service = service, Session = session, SessionProvider = sessionProvider };
        }

        [Fact]
        public async Task SetLightAsync_On_ForwardsDoorbotIdAndTrue()
        {
            Harness h = Build();

            await h.Service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None);

            h.Session.Verify(s => s.SetLight(123L, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetSirenAsync_WithDuration_ForwardsDurationSeconds()
        {
            Harness h = Build();

            await h.Service.SetSirenAsync(DoorbotProviderId, true, 30, CancellationToken.None);

            h.Session.Verify(s => s.SetSiren(123L, true, 30, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetVolumeAsync_ForwardsVolumeAndDeviceName()
        {
            Harness h = Build();

            await h.Service.SetVolumeAsync(DoorbotProviderId, 7, CancellationToken.None);

            h.Session.Verify(s => s.SetVolume(123L, 7, DoorbotName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetNightModeAsync_Enabled_ForwardsDoorbotIdAndTrue()
        {
            Harness h = Build();

            await h.Service.SetNightModeAsync(DoorbotProviderId, true, CancellationToken.None);

            h.Session.Verify(s => s.SetNightMode(123L, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetMotionDetectionAsync_Disabled_ForwardsDoorbotIdAndFalse()
        {
            Harness h = Build();

            await h.Service.SetMotionDetectionAsync(DoorbotProviderId, false, CancellationToken.None);

            h.Session.Verify(s => s.SetMotionDetection(123L, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetDoNotDisturbAsync_ChimeId_ForwardsChimeIdAndSeconds()
        {
            Harness h = Build();

            await h.Service.SetDoNotDisturbAsync(ChimeProviderId, 600, CancellationToken.None);

            h.Session.Verify(s => s.SetDoNotDisturb(456L, 600, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceControlService_SetVolume_UsesStoredNameAndNoRingDeviceList()
        {
            Harness h = Build();

            await h.Service.SetVolumeAsync(DoorbotProviderId, 7, CancellationToken.None);

            h.Session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Never);
            h.Session.Verify(s => s.SetVolume(123L, 7, DoorbotName, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceControlService_SetChimeDoNotDisturb_NoRingDeviceListCall()
        {
            Harness h = Build();

            await h.Service.SetDoNotDisturbAsync(ChimeProviderId, 600, CancellationToken.None);

            h.Session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Never);
            h.Session.Verify(s => s.SetDoNotDisturb(456L, 600, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceControlService_StoredNameBlank_ThrowsBeforeProviderCall()
        {
            List<DeviceEntity> rows = BuildDeviceRows();
            rows[0].Name = "   ";
            Harness h = Build(deviceRows: rows);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => h.Service.SetVolumeAsync(DoorbotProviderId, 7, CancellationToken.None));

            Assert.Equal("Device name is not stored yet; run a sync first", ex.Message);
            h.SessionProvider.Verify(sp => sp.GetSession(It.IsAny<Guid>()), Times.Never);
            h.Session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Never);
            h.Session.Verify(s => s.SetVolume(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetLightAsync_NonNumericDeviceId_ThrowsArgumentException()
        {
            Harness h = Build();

            _ = await Assert.ThrowsAsync<ArgumentException>(() => h.Service.SetLightAsync("not-a-number", true, CancellationToken.None));

            h.Session.Verify(s => s.SetLight(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetLightAsync_UnknownDevice_ThrowsKeyNotFoundAndDoesNotControl()
        {
            Harness h = Build(deviceRows: []);

            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None));

            h.SessionProvider.Verify(sp => sp.GetSession(It.IsAny<Guid>()), Times.Never);
            h.Session.Verify(s => s.SetLight(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetLightAsync_NoSession_ThrowsInvalidOperation()
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns((Session?)null);
            var deviceRepository = new Mock<IDeviceRepository>();
            _ = deviceRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(BuildDeviceRows());
            var locationRepository = new Mock<ILocationRepository>();
            _ = locationRepository.Setup(r => r.ListProviderAccountsForLocationAsync(LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([BuildAccount(RingAccountId, "Ring")]);
            var service = new RingDeviceControlService(new Mock<ILogger>().Object, sessionProvider.Object, deviceRepository.Object, locationRepository.Object);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None));

            session.Verify(s => s.SetLight(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RingDeviceControlService_SingleRingAccount_UsesThatAccountSession()
        {
            Harness h = Build(
            [
                BuildAccount(RingAccountId, "Ring"),
                BuildAccount(WyzeAccountId, "Wyze"),
            ]);

            await h.Service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None);

            h.SessionProvider.Verify(sp => sp.GetSession(RingAccountId), Times.Once);
            h.SessionProvider.Verify(sp => sp.GetSession(WyzeAccountId), Times.Never);
            h.SessionProvider.Verify(sp => sp.GetSession(), Times.Never);
            h.Session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Never);
            h.Session.Verify(s => s.SetLight(123L, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RingDeviceControlService_TwoRingAccounts_ThrowsAndMakesNoProviderCall()
        {
            Harness h = Build(
            [
                BuildAccount(RingAccountId, "Ring"),
                BuildAccount(OtherRingAccountId, "Ring"),
            ]);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => h.Service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None));

            Assert.Equal("Device is linked to more than one Ring account; control refused", ex.Message);
            h.SessionProvider.Verify(sp => sp.GetSession(It.IsAny<Guid>()), Times.Never);
            h.Session.Verify(s => s.GetRingDevices(It.IsAny<Guid?>()), Times.Never);
            h.Session.Verify(s => s.SetLight(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RingDeviceControlService_NoLinkedAccount_ThrowsKeyNotFound()
        {
            Harness h = Build(linkedAccounts: []);

            _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.SetLightAsync(DoorbotProviderId, true, CancellationToken.None));

            h.SessionProvider.Verify(sp => sp.GetSession(It.IsAny<Guid>()), Times.Never);
            h.Session.Verify(s => s.SetLight(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
