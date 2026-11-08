using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DropSnapshotCadenceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotLastRunUtc",
                table: "SyncSchedules");

            migrationBuilder.DropColumn(
                name: "SnapshotNextRunUtc",
                table: "SyncSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotLastRunUtc",
                table: "SyncSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotNextRunUtc",
                table: "SyncSchedules",
                type: "TEXT",
                nullable: true);
        }
    }
}
