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
    /// <summary>
    /// Tests for <see cref="ICaseRepository.ListForProviderAccountAsync"/>.
    /// A case is attached to a provider account when any of its scope devices belongs to a location
    /// the account can see (CaseDevice -> Device -> ProviderAccountLocation).
    /// </summary>
    public class CaseRepositoryProviderAccountTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private ICaseRepository _repository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });

            var actionLog = new ActionLogRepository(_fixture.Factory, _loggerFactory.CreateLogger<ActionLogRepository>());
            _repository = new CaseRepository(_fixture.Factory, actionLog, _loggerFactory.CreateLogger<CaseRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task ListForProviderAccountAsync_DeviceAtVisibleLocation_ReturnsCase()
        {
            ProviderAccount account = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtNewLocationAsync(account.Id);
            ForensicCase forensicCase = await SeedCaseWithDevicesAsync("manual-visible", device.Id);

            IReadOnlyList<ForensicCase> result = await _repository.ListForProviderAccountAsync(account.Id, CancellationToken.None);

            ForensicCase only = Assert.Single(result);
            Assert.Equal(forensicCase.Id, only.Id);
        }

        [Fact]
        public async Task ListForProviderAccountAsync_ScopeDevicesOnlyAtOtherAccountLocations_ExcludesCase()
        {
            ProviderAccount mine = await SeedAccountAsync("Ring");
            ProviderAccount other = await SeedAccountAsync("Wyze");
            Device otherDevice = await SeedDeviceAtNewLocationAsync(other.Id);
            _ = await SeedCaseWithDevicesAsync("manual-other", otherDevice.Id);

            IReadOnlyList<ForensicCase> result = await _repository.ListForProviderAccountAsync(mine.Id, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task ListForProviderAccountAsync_TwoScopeDevicesVisibleToAccount_ReturnsCaseOnce()
        {
            ProviderAccount account = await SeedAccountAsync("Ring");
            Device first = await SeedDeviceAtNewLocationAsync(account.Id);
            Device second = await SeedDeviceAtNewLocationAsync(account.Id);
            ForensicCase forensicCase = await SeedCaseWithDevicesAsync("manual-dupes", first.Id, second.Id);

            IReadOnlyList<ForensicCase> result = await _repository.ListForProviderAccountAsync(account.Id, CancellationToken.None);

            ForensicCase only = Assert.Single(result);
            Assert.Equal(forensicCase.Id, only.Id);
        }

        [Fact]
        public async Task ListForProviderAccountAsync_NoCasesAttached_ReturnsEmpty()
        {
            ProviderAccount account = await SeedAccountAsync("Ring");
            _ = await SeedDeviceAtNewLocationAsync(account.Id);

            IReadOnlyList<ForensicCase> result = await _repository.ListForProviderAccountAsync(account.Id, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task ListForProviderAccountAsync_MultipleCases_OrdersNewestFirst()
        {
            ProviderAccount account = await SeedAccountAsync("Ring");
            Device device = await SeedDeviceAtNewLocationAsync(account.Id);
            ForensicCase older = await SeedCaseWithDevicesAsync("manual-older", device.Id);
            ForensicCase newer = await SeedCaseWithDevicesAsync("manual-newer", device.Id);
            await using (VideoForensicsDbContext db = _fixture.Factory.CreateDbContext())
            {
                ForensicCase olderRow = await db.Cases.SingleAsync(c => c.Id == older.Id);
                olderRow.CreatedAtUtc = DateTime.UtcNow.AddDays(-2);
                ForensicCase newerRow = await db.Cases.SingleAsync(c => c.Id == newer.Id);
                newerRow.CreatedAtUtc = DateTime.UtcNow.AddDays(-1);
                _ = await db.SaveChangesAsync();
            }

            IReadOnlyList<ForensicCase> result = await _repository.ListForProviderAccountAsync(account.Id, CancellationToken.None);

            Assert.Equal(new[] { newer.Id, older.Id }, result.Select(c => c.Id).ToArray());
        }

        private async Task<ProviderAccount> SeedAccountAsync(string providerName)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            User user = TestDataBuilder.BuildUser();
            ProviderAccount account = TestDataBuilder.BuildProviderAccount(user.Id, providerName);
            _ = db.Users.Add(user);
            _ = db.ProviderAccounts.Add(account);
            _ = await db.SaveChangesAsync();
            return account;
        }

        /// <summary>Creates a location linked to the account and a device in it.</summary>
        private async Task<Device> SeedDeviceAtNewLocationAsync(Guid providerAccountId)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            Location location = TestDataBuilder.BuildLocation();
            Device device = TestDataBuilder.BuildDevice(location.Id);
            _ = db.Locations.Add(location);
            _ = db.Devices.Add(device);
            _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation
            {
                ProviderAccountId = providerAccountId,
                LocationId = location.Id
            });
            _ = await db.SaveChangesAsync();
            return device;
        }

        private async Task<ForensicCase> SeedCaseWithDevicesAsync(string caseNumber, params Guid[] deviceIds)
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            var forensicCase = new ForensicCase
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                Title = "Attached case",
                CreatedBy = "tester@example.com",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _ = db.Cases.Add(forensicCase);
            foreach (Guid deviceId in deviceIds)
            {
                _ = db.CaseDevices.Add(new CaseDevice
                {
                    CaseId = forensicCase.Id,
                    DeviceId = deviceId,
                    AddedAtUtc = DateTime.UtcNow
                });
            }

            _ = await db.SaveChangesAsync();
            return forensicCase;
        }
    }
}
