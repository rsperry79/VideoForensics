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
            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalSeconds");

            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalSeconds");

            // Stored values were minutes. Convert them to seconds so the column has one unit everywhere.
            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
            migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds / 60;");
            migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds / 60;");

            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalMinutes");

            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalMinutes");
        }
    }
}
