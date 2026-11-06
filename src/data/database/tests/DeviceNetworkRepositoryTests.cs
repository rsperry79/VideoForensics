using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Round-trips the paid-feature, network-settings and network-diagnosis rows (replace-on-sync, cascade on device delete) through SQLite.</summary>
    public class DeviceNetworkRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceDetailsRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = LoggerFactory.Create(b => { });
            _repository = new DeviceDetailsRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceDetailsRepository>());
            var locationRepository = new LocationRepository(_fixture.Factory, loggerFactory.CreateLogger<LocationRepository>());
            var deviceRepository = new DeviceRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceRepository>());
            Location location = TestDataBuilder.BuildLocation();
            await locationRepository.AddAsync(location, CancellationToken.None);
            _device = TestDataBuilder.BuildDevice(locationId: location.Id);
            await deviceRepository.AddAsync(_device, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static DeviceSettingsChildren Children(RingDevicePaidFeatures? paid, DeviceNetworkSettings? network, RingDeviceNetworkDiagnosis? diagnosis)
        {
            return new DeviceSettingsChildren([], [], [], PaidFeatures: paid, NetworkSettings: network, NetworkDiagnosis: diagnosis);
        }

        private static RingDevicePaidFeatures Paid(bool babyCry)
        {
            return new RingDevicePaidFeatures { CvPaidBabyCry = babyCry, OtherPaidDingCall = true };
        }

        private static DeviceNetworkSettings Network(string wifi24)
        {
            return new DeviceNetworkSettings { MacAddressWifi24 = wifi24, MultiNetPref = 1 };
        }

        private static RingDeviceNetworkDiagnosis Diagnosis(int tcpPort)
        {
            return new RingDeviceNetworkDiagnosis { TcpPort = tcpPort, UdpBandwidth = 1000L };
        }

        [Fact]
        public async Task UpsertAsync_PaidFeatures_WritesOneRowPerDevice()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(true), Network("02:00:00:00:00:02"), Diagnosis(5201)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDevicePaidFeatures paid = await db.RingDevicePaidFeatures.SingleAsync(p => p.DeviceId == _device.Id);
            Assert.True(paid.CvPaidBabyCry);
            Assert.True(paid.OtherPaidDingCall);
            Assert.Null(paid.CvPaidAnimal);
            Assert.NotEqual(default, paid.UpdatedAtUtc);
            DeviceNetworkSettings network = await db.DeviceNetworkSettings.SingleAsync(n => n.DeviceId == _device.Id);
            Assert.Equal("02:00:00:00:00:02", network.MacAddressWifi24);
            Assert.Equal(1, network.MultiNetPref);
            RingDeviceNetworkDiagnosis diagnosis = await db.RingDeviceNetworkDiagnosis.SingleAsync(d => d.DeviceId == _device.Id);
            Assert.Equal(5201, diagnosis.TcpPort);
            Assert.Equal(1000L, diagnosis.UdpBandwidth);
        }

        [Fact]
        public async Task UpsertAsync_SecondCall_ReplacesRowsWithoutDuplicates()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(true), Network("02:00:00:00:00:02"), Diagnosis(5201)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(false), Network("02:00:00:00:00:09"), Diagnosis(6000)));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(1, await db.RingDevicePaidFeatures.CountAsync());
            Assert.False((await db.RingDevicePaidFeatures.SingleAsync()).CvPaidBabyCry);
            Assert.Equal("02:00:00:00:00:09", (await db.DeviceNetworkSettings.SingleAsync()).MacAddressWifi24);
            Assert.Equal(6000, (await db.RingDeviceNetworkDiagnosis.SingleAsync()).TcpPort);
        }

        [Fact]
        public async Task UpsertAsync_NullGroups_LeaveStoredRowsUntouched()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(true), Network("02:00:00:00:00:02"), Diagnosis(5201)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None, new DeviceSettingsChildren([], [], []));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.True((await db.RingDevicePaidFeatures.SingleAsync()).CvPaidBabyCry);
            Assert.Equal("02:00:00:00:00:02", (await db.DeviceNetworkSettings.SingleAsync()).MacAddressWifi24);
            Assert.Equal(5201, (await db.RingDeviceNetworkDiagnosis.SingleAsync()).TcpPort);
        }

        [Fact]
        public async Task UpsertAsync_EmptyGroupRows_WriteRowsOfNulls()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(true), Network("02:00:00:00:00:02"), Diagnosis(5201)));
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(new RingDevicePaidFeatures(), new DeviceNetworkSettings(), new RingDeviceNetworkDiagnosis()));

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            RingDevicePaidFeatures paid = await db.RingDevicePaidFeatures.SingleAsync();
            Assert.Null(paid.CvPaidBabyCry);
            Assert.Null(paid.OtherPaidDingCall);
            DeviceNetworkSettings network = await db.DeviceNetworkSettings.SingleAsync();
            Assert.Null(network.MacAddressWifi24);
            RingDeviceNetworkDiagnosis diagnosis = await db.RingDeviceNetworkDiagnosis.SingleAsync();
            Assert.Null(diagnosis.TcpPort);
        }

        [Fact]
        public async Task DeleteDevice_CascadesToPaidNetworkAndDiagnosisRows()
        {
            await _repository.UpsertAsync(_device.Id, null, null, null, null, CancellationToken.None,
                Children(Paid(true), Network("02:00:00:00:00:02"), Diagnosis(5201)));

            await using (VideoForensicsDbContext write = await _fixture.Factory.CreateDbContextAsync())
            {
                Device d = await write.Devices.SingleAsync(x => x.Id == _device.Id);
                _ = write.Devices.Remove(d);
                _ = await write.SaveChangesAsync();
            }

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.RingDevicePaidFeatures.CountAsync());
            Assert.Equal(0, await db.DeviceNetworkSettings.CountAsync());
            Assert.Equal(0, await db.RingDeviceNetworkDiagnosis.CountAsync());
        }
    }
}
