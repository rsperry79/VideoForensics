using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameSnapshotRssiIntervalToSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalSeconds");

            _ = migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalSeconds");

            // Stored values were minutes. Convert them to seconds so the column has one unit everywhere.
            _ = migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
            _ = migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds / 60;");
            _ = migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds / 60;");

            _ = migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalMinutes");

            _ = migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalMinutes");
        }
    }
}
