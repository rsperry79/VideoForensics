using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHealthEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceHealthRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    WifiSignalRssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastSyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncStatus = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ApiResponseHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRecords_DeviceId",
                table: "DeviceHealthRecords",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceHealthRecords");
        }
    }
}
