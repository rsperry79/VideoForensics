using Microsoft.EntityFrameworkCore;

using System.Data;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    public class MigrationTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task Migration_SchemaCreated_Successfully()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            DataTable tables = await ctx.Database.GetDbConnection().GetSchemaAsync();
            Assert.NotNull(tables);
        }

        [Fact]
        public async Task Migration_Users_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<User> result = await ctx.Users.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ProviderAccounts_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<ProviderAccount> result = await ctx.ProviderAccounts.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Locations_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<Location> result = await ctx.Locations.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Devices_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<Device> result = await ctx.Devices.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_MediaItems_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<MediaItem> result = await ctx.MediaItems.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DownloadEvents_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<DownloadEvent> result = await ctx.DownloadEvents.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DeviceHealths_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<DeviceHealth> result = await ctx.DeviceHealths.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_AiAnalysisSnapshots_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<AiAnalysisSnapshot> result = await ctx.AiAnalysisSnapshots.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Credentials_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<Credential> result = await ctx.Credentials.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ActionLogEntries_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<ActionLogEntry> result = await ctx.ActionLogEntries.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_IntegrityRecords_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<IntegrityRecord> result = await ctx.IntegrityRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Events_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<Event> result = await ctx.Events.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DeviceConfigSnapshots_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<DeviceConfigSnapshot> result = await ctx.DeviceConfigSnapshots.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ProviderReconciliationRecords_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<ProviderReconciliationRecord> result = await ctx.ProviderReconciliationRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ExportRecords_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<ExportRecord> result = await ctx.ExportRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ExportRecordItems_DbSetQueryable()
        {
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            List<ExportRecordItem> result = await ctx.ExportRecordItems.ToListAsync();
            Assert.NotNull(result);
        }
    }
}
