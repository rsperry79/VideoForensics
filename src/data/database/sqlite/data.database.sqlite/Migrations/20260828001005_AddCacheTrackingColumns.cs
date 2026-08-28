using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheTrackingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Devices");
        }
    }
}
