using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

        /// <summary>The four JSON columns with no writer and no value in any live row are not in the model.</summary>
        [Fact]
        public void VideoForensicsDbContext_DeadJsonColumns_AbsentFromModel()
        {
            IModel model = CreateModel();
            (string Table, string Column)[] removed =
            [
                ("Devices", "MetadataJson"),
                ("DeviceCapabilities", "MetadataJson"),
                ("LocationMetadata", "MetadataJson"),
                ("RingAccounts", "MetadataJson"),
            ];

            foreach ((string table, string column) in removed)
            {
                IEntityType entity = Assert.Single(model.GetEntityTypes(), e => e.GetTableName() == table);
                Assert.Null(entity.FindProperty(column));
            }
        }

        /// <summary>The designated unknown-key holders and the columns that still have writers stay in the model.</summary>
        [Fact]
        public void VideoForensicsDbContext_KeptJsonColumns_PresentInModel()
        {
            IModel model = CreateModel();
            (string Table, string Column)[] kept =
            [
                ("Events", "MetadataJson"),
                ("Devices", "SettingsMetadataJson"),
                ("Devices", "LightSettingsMetadataJson"),
                ("Devices", "MotionSnoozeJson"),
                ("DeviceConfigSnapshots", "CustomSettingsJson"),
                ("RingLocationModeNotYetParticipating", "ValueJson"),
            ];

            foreach ((string table, string column) in kept)
            {
                IEntityType entity = Assert.Single(model.GetEntityTypes(), e => e.GetTableName() == table);
                Assert.NotNull(entity.FindProperty(column));
            }
        }

        private static IModel CreateModel()
        {
            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite("DataSource=:memory:", b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"))
                .Options;

            using var ctx = new VideoForensicsDbContext(options);
            return ctx.Model;
        }
    }
}
