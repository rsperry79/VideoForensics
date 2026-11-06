using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Upgrades a database from the migration before the rename and checks the feature rows survive the table rename.</summary>
    public class RenameDeviceFeatureStatesToRingMigrationTests
    {
        private const string PreviousMigration = "20261031051807_AddDeviceLightEvents";

        [Fact]
        public async Task Migrate_FromPreviousMigrationWithRows_KeepsFeatureStatesAndReasons()
        {
            string path = Path.Combine(Path.GetTempPath(), $"vf-rename-{Guid.NewGuid():N}.db");
            string connectionString = $"Data Source={path}";
            try
            {
                DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                    .UseSqlite(connectionString, b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"))
                    .Options;

                var stateId = Guid.NewGuid();
                var deviceId = Guid.NewGuid();
                await using (var ctx = new VideoForensicsDbContext(options))
                {
                    await ctx.GetService<IMigrator>().MigrateAsync(PreviousMigration);
                    Assert.True(await TableExistsAsync(ctx, "DeviceFeatureStates"));

                    _ = await ctx.Database.ExecuteSqlRawAsync(
                        "INSERT INTO DeviceFeatureStates (Id, DeviceId, FeatureKey, Enabled, Score) VALUES ({0}, {1}, 'smart_video_search', 1, 'high')",
                        stateId, deviceId);
                    _ = await ctx.Database.ExecuteSqlRawAsync(
                        "INSERT INTO DeviceFeatureReasons (Id, FeatureStateId, Kind, Position, Reason) VALUES ({0}, {1}, 'disallow', 0, 'not_subscribed')",
                        Guid.NewGuid(), stateId);
                }

                await using (var ctx = new VideoForensicsDbContext(options))
                {
                    await ctx.Database.MigrateAsync();
                    Assert.False(await TableExistsAsync(ctx, "DeviceFeatureStates"));
                    Assert.False(await TableExistsAsync(ctx, "DeviceFeatureReasons"));

                    RingDeviceFeatureState state = await ctx.RingDeviceFeatureStates.Include(s => s.Reasons).SingleAsync();
                    Assert.Equal(stateId, state.Id);
                    Assert.Equal(deviceId, state.DeviceId);
                    Assert.Equal("smart_video_search", state.FeatureKey);
                    Assert.True(state.Enabled);
                    Assert.Equal("high", state.Score);
                    RingDeviceFeatureReason reason = Assert.Single(state.Reasons);
                    Assert.Equal("not_subscribed", reason.Reason);

                    // The cascade FK must still point at the renamed parent.
                    _ = await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
                    _ = await ctx.Database.ExecuteSqlRawAsync("DELETE FROM RingDeviceFeatureStates");
                    Assert.Equal(0, await ctx.RingDeviceFeatureReasons.CountAsync());
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static async Task<bool> TableExistsAsync(VideoForensicsDbContext ctx, string name)
        {
            return await ctx.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {0}", name).SingleAsync() == 1;
        }
    }
}
