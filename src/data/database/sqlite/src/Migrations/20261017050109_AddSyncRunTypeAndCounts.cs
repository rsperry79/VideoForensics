using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRunTypeAndCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfigAreasSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DevicesSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventsSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedsFailed",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationsSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncType",
                table: "ProviderSyncRuns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Unknown");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigAreasSynced",
                table: "ProviderSyncRuns");

            migrationBuilder.DropColumn(
                name: "DevicesSynced",
                table: "ProviderSyncRuns");

            migrationBuilder.DropColumn(
                name: "EventsSynced",
                table: "ProviderSyncRuns");

            migrationBuilder.DropColumn(
                name: "FeedsFailed",
                table: "ProviderSyncRuns");

            migrationBuilder.DropColumn(
                name: "LocationsSynced",
                table: "ProviderSyncRuns");

            migrationBuilder.DropColumn(
                name: "SyncType",
                table: "ProviderSyncRuns");
        }
    }
}
