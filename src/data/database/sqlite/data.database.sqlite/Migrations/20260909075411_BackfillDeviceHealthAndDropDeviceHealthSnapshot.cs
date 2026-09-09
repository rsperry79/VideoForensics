using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDeviceHealthAndDropDeviceHealthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill DeviceHealth from DeviceHealthSnapshot
            // Map: Connected -> IsOnline, Rssi -> WifiSignalRssi
            migrationBuilder.Sql(@"
                INSERT INTO DeviceHealths (Id, DeviceId, BatteryPercentage, WifiSignalRssi, WifiName, IsOnline, FirmwareVersion, CapturedAtUtc)
                SELECT Id, DeviceId, BatteryPercentage, Rssi, WifiName, Connected, FirmwareVersion, CapturedAtUtc
                FROM DeviceHealthSnapshots
                WHERE DeviceId IS NOT NULL;
            ");

            // Drop the old table
            migrationBuilder.DropTable(
                name: "DeviceHealthSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the DeviceHealthSnapshot table
            migrationBuilder.CreateTable(
                name: "DeviceHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadEventId = table.Column<string>(type: "TEXT", nullable: true),
                    Connected = table.Column<bool>(type: "INTEGER", nullable: true),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    Rssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CapturedAtUtc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthSnapshots", x => x.Id);
                });

            // Backfill DeviceHealthSnapshot from DeviceHealth (reverse operation)
            migrationBuilder.Sql(@"
                INSERT INTO DeviceHealthSnapshots (Id, DeviceId, Connected, BatteryPercentage, Rssi, WifiName, FirmwareVersion, CapturedAtUtc)
                SELECT Id, DeviceId, IsOnline, BatteryPercentage, WifiSignalRssi, WifiName, FirmwareVersion, CapturedAtUtc
                FROM DeviceHealths;
            ");
        }
    }
}
