using Microsoft.Extensions.Logging;
using Xunit;
using Microsoft.EntityFrameworkCore;

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
            var ctx = _fixture.Factory.CreateDbContext();
            var tables = await ctx.Database.GetDbConnection().GetSchemaAsync();
            Assert.NotNull(tables);
        }

        [Fact]
        public async Task Migration_Users_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Users.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ProviderAccounts_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.ProviderAccounts.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Locations_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Locations.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Devices_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Devices.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_MediaItems_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.MediaItems.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DownloadEvents_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.DownloadEvents.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DeviceHealthSnapshots_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.DeviceHealthSnapshots.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_AiAnalysisSnapshots_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.AiAnalysisSnapshots.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Credentials_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Credentials.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ActionLogEntries_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.ActionLogEntries.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_IntegrityRecords_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.IntegrityRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Events_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Events.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_DeviceConfigSnapshots_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.DeviceConfigSnapshots.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_Annotations_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.Annotations.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ProviderReconciliationRecords_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.ProviderReconciliationRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ExportRecords_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.ExportRecords.ToListAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Migration_ExportRecordItems_DbSetQueryable()
        {
            var ctx = _fixture.Factory.CreateDbContext();
            var result = await ctx.ExportRecordItems.ToListAsync();
            Assert.NotNull(result);
        }
    }
}
