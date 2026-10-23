using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using System.Reflection;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>
    /// Verifies the MakeDeviceIsOnlineNullable migration and the model snapshot without touching a live database.
    /// Types are resolved by name so these tests fail at runtime (not compile time) until the migration exists.
    /// </summary>
    public class DeviceIsOnlineNullableMigrationTests
    {
        private const string MigrationTypeName = "VideoForensics.Data.Database.Sqlite.Migrations.MakeDeviceIsOnlineNullable";
        private const string SnapshotTypeName = "VideoForensics.Data.Database.Sqlite.Migrations.VideoForensicsDbContextModelSnapshot";

        private static Assembly SqliteAssembly => typeof(VideoForensics.Data.Database.Sqlite.DependencyInjection.ServiceCollectionExtensions).Assembly;

        [Fact]
        public void Migration_MakeDeviceIsOnlineNullable_Exists()
        {
            Type? migrationType = SqliteAssembly.GetType(MigrationTypeName);

            Assert.NotNull(migrationType);
            Assert.True(typeof(Migration).IsAssignableFrom(migrationType));
        }

        [Fact]
        public void Migration_MakeDeviceIsOnlineNullable_AltersDevicesIsOnlineToNullable()
        {
            Type? migrationType = SqliteAssembly.GetType(MigrationTypeName);
            Assert.NotNull(migrationType);

            var migration = (Migration)Activator.CreateInstance(migrationType)!;
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            MethodInfo up = migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            _ = up.Invoke(migration, [builder]);

            AlterColumnOperation alter = Assert.Single(builder.Operations.OfType<AlterColumnOperation>());
            Assert.Equal("Devices", alter.Table);
            Assert.Equal("IsOnline", alter.Name);
            Assert.True(alter.IsNullable);
        }

        [Fact]
        public void Snapshot_DeviceIsOnline_IsNullable()
        {
            Type? snapshotType = SqliteAssembly.GetType(SnapshotTypeName);
            Assert.NotNull(snapshotType);

            object snapshot = Activator.CreateInstance(snapshotType, nonPublic: true)!;
            var model = (IModel)snapshotType.GetProperty("Model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(snapshot)!;

            IEntityType device = model.FindEntityType("VideoForensics.Data.Common.Entities.Device")!;
            Assert.NotNull(device);
            IProperty isOnline = device.FindProperty("IsOnline")!;
            Assert.True(isOnline.IsNullable);
        }
    }
}
