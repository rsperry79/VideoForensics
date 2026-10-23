using System.Reflection;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Verifies the AddProviderSyncRunSnapshotsSynced migration only adds a nullable column and never drops or alters data.</summary>
    public class AddProviderSyncRunSnapshotsSyncedMigrationTests
    {
        private const string MigrationTypeName = "VideoForensics.Data.Database.Sqlite.Migrations.AddProviderSyncRunSnapshotsSynced";

        private static Assembly SqliteAssembly => typeof(VideoForensics.Data.Database.Sqlite.DependencyInjection.ServiceCollectionExtensions).Assembly;

        private static MigrationBuilder BuildUp()
        {
            Type? migrationType = SqliteAssembly.GetType(MigrationTypeName);
            Assert.NotNull(migrationType);

            var migration = (Migration)Activator.CreateInstance(migrationType)!;
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            MethodInfo up = migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            _ = up.Invoke(migration, [builder]);
            return builder;
        }

        [Fact]
        public void Migration_AddProviderSyncRunSnapshotsSynced_Exists()
        {
            Type? migrationType = SqliteAssembly.GetType(MigrationTypeName);

            Assert.NotNull(migrationType);
            Assert.True(typeof(Migration).IsAssignableFrom(migrationType));
        }

        [Fact]
        public void Migration_AddProviderSyncRunSnapshotsSynced_UpContainsOnlyAddColumnOperations()
        {
            MigrationBuilder builder = BuildUp();

            Assert.NotEmpty(builder.Operations);
            Assert.All(builder.Operations, op => Assert.IsType<AddColumnOperation>(op));
        }

        [Fact]
        public void Migration_AddProviderSyncRunSnapshotsSynced_AddsNullableSnapshotCountColumnToProviderSyncRuns()
        {
            MigrationBuilder builder = BuildUp();

            AddColumnOperation column = Assert.Single(builder.Operations.OfType<AddColumnOperation>());
            Assert.Equal("ProviderSyncRuns", column.Table);
            Assert.Equal("SnapshotsSynced", column.Name);
            Assert.True(column.IsNullable);
        }
    }
}
