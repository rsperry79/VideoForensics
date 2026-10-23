using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using System.Reflection;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Verifies the AddEventsNotPulled migration only adds nullable columns and never drops or alters data.</summary>
    public class AddEventsNotPulledMigrationTests
    {
        private const string MigrationTypeName = "VideoForensics.Data.Database.Sqlite.Migrations.AddEventsNotPulled";

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
        public void Migration_AddEventsNotPulled_Exists()
        {
            Type? migrationType = SqliteAssembly.GetType(MigrationTypeName);

            Assert.NotNull(migrationType);
            Assert.True(typeof(Migration).IsAssignableFrom(migrationType));
        }

        [Fact]
        public void Migration_AddEventsNotPulled_UpContainsOnlyAddColumnOperations()
        {
            MigrationBuilder builder = BuildUp();

            Assert.NotEmpty(builder.Operations);
            Assert.All(builder.Operations, op => Assert.IsType<AddColumnOperation>(op));
        }

        [Fact]
        public void Migration_AddEventsNotPulled_AddsNullableCountColumnsToProviderSyncRuns()
        {
            MigrationBuilder builder = BuildUp();

            var columns = builder.Operations.OfType<AddColumnOperation>().ToList();
            Assert.Equal(2, columns.Count);
            Assert.All(columns, c => Assert.Equal("ProviderSyncRuns", c.Table));
            Assert.All(columns, c => Assert.True(c.IsNullable));
            Assert.Contains(columns, c => c.Name == "EventsNotPulled");
            Assert.Contains(columns, c => c.Name == "EventsAlreadyPresent");
        }
    }
}
