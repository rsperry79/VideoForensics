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
            _ = migrationBuilder.AddColumn<int>(
                name: "ConfigAreasSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "DevicesSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "EventsSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "FeedsFailed",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "LocationsSynced",
                table: "ProviderSyncRuns",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
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
            _ = migrationBuilder.DropColumn(
                name: "ConfigAreasSynced",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "DevicesSynced",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "EventsSynced",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "FeedsFailed",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "LocationsSynced",
                table: "ProviderSyncRuns");

            _ = migrationBuilder.DropColumn(
                name: "SyncType",
                table: "ProviderSyncRuns");
        }
    }
}
