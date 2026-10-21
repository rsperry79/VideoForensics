using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncScheduleSnapshotRollup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRollupUtc",
                table: "SyncSchedules",
                type: "TEXT",
                nullable: true);

            // Existing schedules get the 15-minute default; 0 would fail the 1-minute minimum on their next save.
            migrationBuilder.AddColumn<int>(
                name: "SnapshotRollupIntervalMinutes",
                table: "SyncSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRollupUtc",
                table: "SyncSchedules");

            migrationBuilder.DropColumn(
                name: "SnapshotRollupIntervalMinutes",
                table: "SyncSchedules");
        }
    }
}
