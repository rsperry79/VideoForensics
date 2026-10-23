using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Providers.Ring.Services;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;
using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>
    /// The snapshot scheduler against a real database: seeded account, location and device rows, the real repositories,
    /// and a fake provider. Proves a due camera is captured, the attempt row is written, and the interval holds the next tick.
    /// The mock-based tests in SnapshotCaptureServiceTests cannot catch a repository or query bug, so this one does.
    /// </summary>
    public class SnapshotCaptureSchedulerDbTests : IDisposable
    {
        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03];

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-snapshot-scheduler-" + Guid.NewGuid().ToString("N"));
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "vf-snapshot-scheduler-" + Guid.NewGuid().ToString("N") + ".db");
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Guid _deviceId = Guid.NewGuid();

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
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
                // The SQLite file can stay locked until the host is disposed; the temp folder is cleared by the OS later.
            }
        }

        [Fact]
        public async Task Scheduler_DueCamera_CapturesOnceAndWaitsForTheInterval()
        {
            var capture = new Mock<IProviderSnapshotCapture>();
            _ = capture.Setup(c => c.CaptureAsync(_accountId, "100", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, Jpeg, null));

            await using ServiceProvider provider = await BuildProviderAsync(capture.Object);
            SnapshotCaptureService scheduler = BuildScheduler(provider);

            // First tick: the camera has no attempt in the window, so it is due and captured.
            await scheduler.RunOneTickAsync(CancellationToken.None);
            // Second tick straight after: the attempt is inside the 60 s default interval, so nothing is captured.
            await scheduler.RunOneTickAsync(CancellationToken.None);

            capture.Verify(c => c.CaptureAsync(_accountId, "100", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);

            await using VideoForensicsDbContext db = await provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>().CreateDbContextAsync();
            List<SnapshotCapture> rows = await db.SnapshotCaptures.Where(r => r.DeviceId == _deviceId).ToListAsync();
            SnapshotCapture row = Assert.Single(rows);
            Assert.Equal(200, row.HttpStatus);
            Assert.NotNull(row.MediaFilePath);
            // MediaFilePath is relative to SnapshotRoot; the JPEG must exist under it.
            Assert.True(File.Exists(Path.Combine(_root, row.MediaFilePath!)));
        }

        private SnapshotCaptureService BuildScheduler(IServiceProvider provider)
        {
            return new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SnapshotTimelineOptions { SnapshotRoot = _root }),
            NullLogger<SnapshotCaptureService>.Instance,
            TimeProvider.System);
        }

        /// <summary>Builds the real data layer on a temp database, seeds one Ring account with one camera, and fakes only the provider.</summary>
        private async Task<ServiceProvider> BuildProviderAsync(IProviderSnapshotCapture capture)
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddVideoForensicsDataLayer(_dbPath);
            _ = services.AddKeyedSingleton<IProviderSnapshotCapture>("Ring", capture);
            var auth = new Mock<IProviderAuthService>();
            _ = auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = services.AddKeyedSingleton<IProviderAuthService>("Ring", auth.Object);
            _ = services.AddKeyedSingleton<IProviderDeviceStatus>("Ring", new RingDeviceOnlineStatus());
            _ = services.AddSingleton(Mock.Of<ISessionProvider>());

            ServiceProvider provider = services.BuildServiceProvider();
            IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            await using (VideoForensicsDbContext db = await factory.CreateDbContextAsync())
            {
                _ = await db.Database.EnsureCreatedAsync();
                var userId = Guid.NewGuid();
                _ = db.ProviderAccounts.Add(new ProviderAccountEntity { Id = _accountId, UserId = userId, ProviderName = "Ring", IsActive = true, LinkedUtc = DateTime.UtcNow });
                _ = db.Locations.Add(new Location { Id = _locationId, ProviderLocationId = "loc-snapshot", Name = "Home" });
                _ = db.Devices.Add(new Device
                {
                    Id = _deviceId,
                    LocationId = _locationId,
                    ProviderDeviceId = "100",
                    Name = "Front Door",
                    Type = "doorbot",
                    IsOnline = true,
                    SnapshotCaptureEnabled = true,
                });
                _ = db.ProviderAccountLocations.Add(new ProviderAccountLocation { ProviderAccountId = _accountId, LocationId = _locationId });
                _ = await db.SaveChangesAsync();
            }

            return provider;
        }
    }
}
