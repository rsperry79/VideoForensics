using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>The unit-of-work device list fills ProviderName the same way the device repository does.</summary>
    public class UnitOfWorkDeviceProviderNameTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var services = new ServiceCollection();
            _ = services.AddSingleton<ILogger<UnitOfWork>>(new Logger<UnitOfWork>(loggerFactory));
            _unitOfWork = new UnitOfWork(_fixture.Factory, services.BuildServiceProvider(), loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkDeviceRepository_ListAsync_LinkedLocation_SetsProviderName()
        {
            Guid accountId;
            Device device;
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                User user = TestDataBuilder.BuildUser();
                ProviderAccount account = TestDataBuilder.BuildProviderAccount(user.Id, "Ring");
                Location location = TestDataBuilder.BuildLocation();
                device = TestDataBuilder.BuildDevice(location.Id);
                accountId = account.Id;
                _ = db.Users.Add(user);
                _ = db.ProviderAccounts.Add(account);
                _ = db.Locations.Add(location);
                _ = db.Devices.Add(device);
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = accountId, LocationId = location.Id });
                _ = await db.SaveChangesAsync();
            }

            IReadOnlyList<Device> devices = await _unitOfWork.ExecuteAsync(ctx => ctx.Devices.ListAsync(CancellationToken.None), CancellationToken.None);

            Assert.Equal("Ring", Assert.Single(devices, d => d.Id == device.Id).ProviderName);
        }
    }
}
