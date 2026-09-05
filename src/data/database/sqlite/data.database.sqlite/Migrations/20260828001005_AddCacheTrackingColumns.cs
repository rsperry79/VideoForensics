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
            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Locations",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            _ = migrationBuilder.AddColumn<string>(
                name: "ApiResponseHash",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SyncStatus",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Locations");

            _ = migrationBuilder.DropColumn(
                name: "ApiResponseHash",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "Devices");
        }
    }
}
