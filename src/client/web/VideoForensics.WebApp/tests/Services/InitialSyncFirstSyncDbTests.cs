using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.WebApp.Tests.Services
{
    /// <summary>
    /// The first sync against a real database: seeded Ring account, location and camera, the real runner, coordinator and
    /// repositories, and a fake provider. Proves the InitialSyncCompleted flag and the run row end up in the database.
    /// </summary>
    public class InitialSyncFirstSyncDbTests : IAsyncLifetime
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "vf-first-sync-" + Guid.NewGuid().ToString("N") + ".db");
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Guid _deviceId = Guid.NewGuid();
        private ServiceProvider? _provider;

        public ValueTask InitializeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider is not null)
            {
                await _provider.DisposeAsync();
            }

            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch (IOException)
            {
                // SQLite can keep the file locked briefly; the OS clears the temp folder later.
            }
        }

        [Fact]
        public async Task FirstSync_AllStagesSucceed_SetsFlagAndRecordsSuccessfulFullRun()
        {
            var pull = new Mock<IEventPullService>();
            _ = pull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult(DevicesSynced: 1, LocationsSynced: 1));
            _provider = await BuildProviderAsync(pull.Object, integrityThrows: false);

            await RunFirstSyncAsync();

            await using VideoForensicsDbContext db = await Db();
            Assert.True((await db.ProviderAccounts.FindAsync(_accountId))!.InitialSyncCompleted);
            ProviderSyncRun run = Assert.Single(await db.ProviderSyncRuns.Where(r => r.ProviderAccountId == _accountId).ToListAsync());
            Assert.True(run.Success);
            // "Full" is the sync type recorded when the pull covered the whole history (ProviderSyncRunRecorder.FullSyncType).
            Assert.Equal("Full", run.SyncType);
        }

        [Fact]
        public async Task FirstSync_PullRecordsError_LeavesFlagFalseAndRecordsFailedRun()
        {
            var pull = new Mock<IEventPullService>();
            _ = pull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    // The real pull records its own failures on the account instead of throwing.
                    await using VideoForensicsDbContext db = await Db();
                    ProviderAccount account = (await db.ProviderAccounts.FindAsync(_accountId))!;
                    account.LastErrorMessage = "Ring feed timed out";
                    account.LastErrorUtc = DateTime.UtcNow;
                    _ = await db.SaveChangesAsync();
                    return new EventPullResult();
                });
            _provider = await BuildProviderAsync(pull.Object, integrityThrows: false);

            await RunFirstSyncAsync();

            await using VideoForensicsDbContext check = await Db();
            Assert.False((await check.ProviderAccounts.FindAsync(_accountId))!.InitialSyncCompleted);
            ProviderSyncRun run = Assert.Single(await check.ProviderSyncRuns.Where(r => r.ProviderAccountId == _accountId).ToListAsync());
            Assert.False(run.Success);
        }

        [Fact]
        public async Task FirstSync_MediaValidationFails_LeavesFlagFalse()
        {
            var pull = new Mock<IEventPullService>();
            _ = pull.Setup(p => p.PullAccountEventsAsync(_accountId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new EventPullResult());
            _provider = await BuildProviderAsync(pull.Object, integrityThrows: true);

            await RunFirstSyncAsync();

            await using VideoForensicsDbContext db = await Db();
            Assert.False((await db.ProviderAccounts.FindAsync(_accountId))!.InitialSyncCompleted);
        }

        private async Task RunFirstSyncAsync()
        {
            using IServiceScope scope = _provider!.GetRequiredService<IServiceScopeFactory>().CreateScope();
            await scope.ServiceProvider.GetRequiredService<IInitialSyncCoordinator>().RunAsync(_accountId, CancellationToken.None);
        }

        private async Task<VideoForensicsDbContext> Db()
        {
            return await _provider!.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>().CreateDbContextAsync();
        }

        /// <summary>Real data layer and coordinator on a temp database; only the provider (auth, pull, media integrity) is faked.</summary>
        private async Task<ServiceProvider> BuildProviderAsync(IEventPullService pull, bool integrityThrows)
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddVideoForensicsDataLayer(_dbPath);
            _ = services.AddSingleton<ISyncActivityLog>(new SyncActivityLog());

            var auth = new Mock<IProviderAuthService>();
            _ = auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = services.AddKeyedSingleton<IProviderAuthService>("Ring", auth.Object);
            _ = services.AddKeyedSingleton<IEventPullService>("Ring", pull);
            _ = services.AddSingleton(Mock.Of<ISnapshotSyncService>());

            var validation = new Mock<IEvidenceValidationService>();
            _ = integrityThrows
                ? validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                    .ThrowsAsync(new InvalidOperationException("disk offline"))
                : validation.Setup(v => v.VerifyLocalIntegrityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
                    .ReturnsAsync([]);

            _ = validation.Setup(v => v.ReconcileWithProviderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _ = services.AddSingleton(validation.Object);

            _ = services.AddScoped<IAccountSyncRunner, AccountSyncRunner>();
            _ = services.AddScoped<BulkValidationService>();
            _ = services.AddScoped<IInitialSyncCoordinator, InitialSyncCoordinator>();

            ServiceProvider provider = services.BuildServiceProvider();
            IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            await using (VideoForensicsDbContext db = await factory.CreateDbContextAsync())
            {
                _ = await db.Database.EnsureCreatedAsync();
                _ = db.ProviderAccounts.Add(new ProviderAccount
                {
                    Id = _accountId,
                    UserId = Guid.NewGuid(),
                    ProviderName = "Ring",
                    IsActive = true,
                    LinkedUtc = DateTime.UtcNow,
                    InitialSyncCompleted = false,
                });
                _ = db.Locations.Add(new Location { Id = _locationId, ProviderLocationId = "loc-first-sync", Name = "Home" });
                _ = db.Devices.Add(new Device { Id = _deviceId, LocationId = _locationId, ProviderDeviceId = "100", Name = "Front Door", Type = "doorbot" });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = _accountId, LocationId = _locationId });
                _ = await db.SaveChangesAsync();
            }

            return provider;
        }
    }
}
