using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Download-status fields written by the download path must survive a re-upsert of an event that discovery
    /// already inserted. The UnitOfWork event repository is the write path the download service uses.
    /// </summary>
    public class UnitOfWorkEventDownloadTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private UnitOfWork _unitOfWork = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, _loggerFactory);
            _unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, _loggerFactory.CreateLogger<UnitOfWork>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task UnitOfWorkEventRepository_UpsertExistingEvent_KeepsDownloadFields()
        {
            var deviceId = Guid.NewGuid();
            const string providerEventId = "evt_download_001";
            const string integrityHash = "integrity-hash-001";
            var downloadedAtUtc = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

            // Discovery inserts the event with no download fields.
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Event discovered = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                _ = await context.Events.UpsertAsync(discovered, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            // The download path re-upserts the same event with its download results.
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                Event downloaded = TestDataBuilder.BuildEvent(deviceId, providerEventId);
                downloaded.EventIntegrityHash = integrityHash;
                downloaded.DownloadedAtUtc = downloadedAtUtc;
                _ = await context.Events.UpsertAsync(downloaded, CancellationToken.None);
                return null;
            }, CancellationToken.None);

            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            Event stored = await ctx.Events.SingleAsync(e => e.DeviceId == deviceId && e.ProviderEventId == providerEventId);
            Assert.Equal(integrityHash, stored.EventIntegrityHash);
            Assert.Equal(downloadedAtUtc, stored.DownloadedAtUtc);
            Assert.Equal(EventDownloadStatus.Downloaded, stored.DownloadStatus);
        }
    }
}
