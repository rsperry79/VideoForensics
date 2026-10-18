using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Hosting.Tests
{
    public sealed class AccountExportServiceTests : IDisposable
    {
        private const string SecretValue = "SUPER-SECRET-TOKEN-VALUE-42";

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-export-" + Guid.NewGuid().ToString("N"));
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<ICredentialRepository> _credentials = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<IEventRepository> _events = new();
        private readonly Mock<IMediaItemRepository> _media = new();
        private readonly Mock<IDownloadEventRepository> _downloads = new();
        private readonly Mock<IProviderSyncRunRepository> _syncRuns = new();
        private readonly ProviderAccount _account;
        private readonly string _exportsFolder;

        public AccountExportServiceTests()
        {
            _exportsFolder = Path.Combine(_root, "exports");
            _account = new ProviderAccount
            {
                Id = Guid.NewGuid(),
                ProviderName = "Ring/Home:1",
                LinkedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                RemovedUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false
            };
            _ = _accounts.Setup(r => r.GetAsync(_account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            _ = _credentials.Setup(r => r.GetByProviderAccountIdAsync(_account.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Credential { Id = Guid.NewGuid(), ProviderAccountId = _account.Id, CredentialType = "refresh_token", EncryptedValue = SecretValue, EncryptionProvider = "aes" }]);

            var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            var device = new Device { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };
            _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(_account.Id, It.IsAny<CancellationToken>())).ReturnsAsync([location]);
            _ = _devices.Setup(r => r.GetByLocationIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([device]);
            _ = _events.Setup(r => r.ListByDeviceAndDateRangeAsync(device.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Event { Id = Guid.NewGuid(), DeviceId = device.Id, ProviderEventId = "evt-marker-1", EventType = "motion" }]);
            _ = _media.Setup(r => r.GetByDeviceIdAsync(device.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new MediaItem { Id = Guid.NewGuid(), DeviceId = device.Id, FileName = "clip.mp4", FilePath = "x/clip.mp4", MediaFormat = "mp4", Sha256Hash = "abc" }]);
            _ = _downloads.Setup(r => r.GetByDeviceIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _ = _syncRuns.Setup(r => r.ListByProviderAccountIdAsync(_account.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, true);
                }
            }
            catch (IOException)
            {
            }
        }

        private AccountExportService CreateSut()
        {
            _ = Directory.CreateDirectory(_root);
            var storage = new Mock<IStorageLocationProvider>();
            _ = storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Exports, It.IsAny<string?>())).Returns(_exportsFolder);
            var config = new Mock<IForensicsConfiguration>();
            _ = config.SetupGet(c => c.ExportsLocation).Returns((string?)null);
            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(c => c.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(config.Object);

            var services = new ServiceCollection();
            _ = services.AddSingleton(_accounts.Object).AddSingleton(_credentials.Object).AddSingleton(_locations.Object)
                .AddSingleton(_devices.Object).AddSingleton(_events.Object).AddSingleton(_media.Object)
                .AddSingleton(_downloads.Object).AddSingleton(_syncRuns.Object)
                .AddSingleton(storage.Object).AddSingleton(configService.Object);
            ServiceProvider sp = services.BuildServiceProvider();
            return new AccountExportService(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AccountExportService>.Instance, TimeProvider.System);
        }

        private static async Task<ExportJobStatus> RunAsync(AccountExportService sut, Guid accountId, bool purge)
        {
            Guid jobId = await sut.QueueExportAsync(accountId, purge, CancellationToken.None);
            await sut.RunJobAsync(jobId, CancellationToken.None);
            return (await sut.GetStatusAsync(jobId, CancellationToken.None))!;
        }

        [Fact]
        public async Task AccountExportService_QueueExportAsync_ReturnsJobIdAndQueuedStatus()
        {
            AccountExportService sut = CreateSut();

            Guid jobId = await sut.QueueExportAsync(_account.Id, true, CancellationToken.None);

            Assert.NotEqual(Guid.Empty, jobId);
            ExportJobStatus? status = await sut.GetStatusAsync(jobId, CancellationToken.None);
            Assert.NotNull(status);
            Assert.Equal(ExportJobState.Queued, status.State);
            Assert.Equal(_account.Id, status.AccountId);
            Assert.True(status.PurgeAfterExport);
            _accounts.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AccountExportService_RunJob_WritesJsonFileToExportsFolder()
        {
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, false);

            Assert.Equal(ExportJobState.Succeeded, status.State);
            Assert.NotNull(status.FilePath);
            Assert.True(File.Exists(status.FilePath));
            Assert.Equal(_exportsFolder, Path.GetDirectoryName(status.FilePath));
            Assert.EndsWith(".json", status.FilePath);
            Assert.Contains(_account.Id.ToString("D")[..8], Path.GetFileName(status.FilePath));
            Assert.Empty(Directory.GetFiles(_exportsFolder, "*.tmp"));
            string json = await File.ReadAllTextAsync(status.FilePath);
            Assert.Contains("evt-marker-1", json);
            Assert.Contains("clip.mp4", json);
            Assert.Contains("providerName", json);
        }

        [Fact]
        public async Task AccountExportService_RunJob_NeverWritesCredentialsOrTokens()
        {
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, false);

            string json = await File.ReadAllTextAsync(status.FilePath!);
            Assert.DoesNotContain(SecretValue, json);
            _credentials.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AccountExportService_RunJob_PurgeAfterExport_DeletesAccountOnlyAfterSuccess()
        {
            bool fileExistedAtDelete = false;
            _ = _accounts.Setup(r => r.DeleteAsync(_account.Id, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .Callback(() => fileExistedAtDelete = Directory.GetFiles(_exportsFolder, "*.json").Length == 1)
                .Returns(Task.CompletedTask);
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, true);

            Assert.Equal(ExportJobState.Succeeded, status.State);
            Assert.True(fileExistedAtDelete);
            _accounts.Verify(r => r.DeleteAsync(_account.Id, It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task AccountExportService_RunJob_NoPurge_DoesNotDeleteAccount()
        {
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, false);

            Assert.Equal(ExportJobState.Succeeded, status.State);
            _accounts.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountExportService_RunJob_ExportFails_AccountIsKeptAndJobFailed()
        {
            // A regular file where the exports folder should be makes directory creation / writing fail.
            _ = Directory.CreateDirectory(_root);
            await File.WriteAllTextAsync(_exportsFolder, "not a directory");
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, true);

            Assert.Equal(ExportJobState.Failed, status.State);
            Assert.False(string.IsNullOrWhiteSpace(status.ErrorMessage));
            Assert.Null(status.FilePath);
            _accounts.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountExportService_RunJob_AccountMissing_FailsWithNotFound()
        {
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, Guid.NewGuid(), true);

            Assert.Equal(ExportJobState.Failed, status.State);
            Assert.Equal("Account not found", status.ErrorMessage);
            _accounts.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AccountExportService_RunJob_PurgeDeleteThrows_JobFailsExportFileKept()
        {
            _ = _accounts.Setup(r => r.DeleteAsync(_account.Id, It.IsAny<CancellationToken>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException("delete boom"));
            AccountExportService sut = CreateSut();

            ExportJobStatus status = await RunAsync(sut, _account.Id, true);

            Assert.Equal(ExportJobState.Failed, status.State);
            Assert.Equal("delete boom", status.ErrorMessage);
            _ = Assert.Single(Directory.GetFiles(_exportsFolder, "*.json"));
        }
    }
}
