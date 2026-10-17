using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Persists Ring's health id and supported RPC command list with each DeviceHealths row.</summary>
    public class DeviceHealthRpcCommandRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private DeviceHealthRepository _repository = null!;
        private Device _device = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new DeviceHealthRepository(_fixture.Factory, loggerFactory.CreateLogger<DeviceHealthRepository>());
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

        [Fact]
        public async Task AddAsync_SupportedRpcCommands_WritesChildRowsInRingOrder()
        {
            DeviceHealth health = TestDataBuilder.BuildDeviceHealth(_device.Id);
            health.RingHealthId = 77;
            health.SupportedRpcCommands =
            [
                new DeviceHealthRpcCommand { Id = Guid.NewGuid(), Position = 0, Command = "reboot" },
                new DeviceHealthRpcCommand { Id = Guid.NewGuid(), Position = 1, Command = "siren" }
            ];

            _ = await _repository.AddAsync(health, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            DeviceHealth stored = await db.DeviceHealths.SingleAsync(h => h.Id == health.Id);
            Assert.Equal(77L, stored.RingHealthId);
            List<DeviceHealthRpcCommand> commands = await db.DeviceHealthRpcCommands
                .Where(c => c.DeviceHealthId == health.Id)
                .OrderBy(c => c.Position)
                .ToListAsync();
            Assert.Equal(["reboot", "siren"], commands.Select(c => c.Command));
        }

        [Fact]
        public async Task AddAsync_NoRpcCommands_WritesNoChildRows()
        {
            DeviceHealth health = TestDataBuilder.BuildDeviceHealth(_device.Id);

            _ = await _repository.AddAsync(health, CancellationToken.None);

            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.DeviceHealthRpcCommands.CountAsync());
            Assert.Null((await db.DeviceHealths.SingleAsync(h => h.Id == health.Id)).RingHealthId);
        }
    }
}
