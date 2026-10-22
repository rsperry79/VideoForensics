using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Verifies the EF model matches the migrations model snapshot (no pending model changes).</summary>
    public class ModelMatchesMigrationsTests
    {
        [Fact]
        public async Task VideoForensicsDbContext_ModelMatchesMigrations_NoPendingModelChanges()
        {
            // The migrations assembly must be set: without it EF has no snapshot to compare against.
            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite("DataSource=:memory:", b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"))
                .Options;

            await using var ctx = new VideoForensicsDbContext(options);
            Assert.False(ctx.Database.HasPendingModelChanges(),
                "Entity model differs from the migrations model snapshot; add a migration or fix the entity configuration.");
        }
    }
}
