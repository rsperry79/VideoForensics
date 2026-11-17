using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameSnapshotIntervalToMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalMinutes");

            migrationBuilder.RenameColumn(
                name: "SnapshotIntervalSeconds",
                table: "SyncSchedules",
                newName: "SnapshotIntervalMinutes");

            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalSeconds",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalMinutes");

            // Stored values were seconds. The rate-limit floor rounds up so a 5-second back-off becomes one minute, never zero.
            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotIntervalMinutes = MAX(1, (SnapshotIntervalMinutes + 59) / 60) WHERE SnapshotIntervalMinutes IS NOT NULL;");
            migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalMinutes = MAX(1, (SnapshotRssiIntervalMinutes + 59) / 60);");

            // Operator decision: every existing account moves to the new two-minute default.
            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalMinutes = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "SyncSchedules",
                newName: "SnapshotRssiIntervalSeconds");

            migrationBuilder.RenameColumn(
                name: "SnapshotIntervalMinutes",
                table: "SyncSchedules",
                newName: "SnapshotIntervalSeconds");

            migrationBuilder.RenameColumn(
                name: "SnapshotRssiIntervalMinutes",
                table: "JammingScheduleWindows",
                newName: "SnapshotRssiIntervalSeconds");

            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
            migrationBuilder.Sql("UPDATE SyncSchedules SET SnapshotIntervalSeconds = SnapshotIntervalSeconds * 60 WHERE SnapshotIntervalSeconds IS NOT NULL;");
            migrationBuilder.Sql("UPDATE JammingScheduleWindows SET SnapshotRssiIntervalSeconds = SnapshotRssiIntervalSeconds * 60;");
        }
    }
}
