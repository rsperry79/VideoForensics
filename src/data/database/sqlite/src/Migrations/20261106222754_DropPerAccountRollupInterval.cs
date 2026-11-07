using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropPerAccountRollupInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRollupUtc",
                table: "SyncSchedules");

            migrationBuilder.DropColumn(
                name: "SnapshotRollupIntervalMinutes",
                table: "SyncSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRollupUtc",
                table: "SyncSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnapshotRollupIntervalMinutes",
                table: "SyncSchedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 15);
        }
    }
}
