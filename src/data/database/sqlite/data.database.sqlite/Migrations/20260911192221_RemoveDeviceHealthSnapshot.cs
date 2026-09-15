using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceHealthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.Sql(@"
    INSERT INTO DeviceHealths (Id, DeviceId, BatteryPercentage, BatteryVoltageValue, WifiSignalRssi, WifiName, IsExternalPowerConnected, OtaStatus, IsOnline, LastHeartbeatUtc, CapturedAtUtc, FirmwareVersion)
    SELECT Id, DeviceId, BatteryPercentage, NULL, Rssi, WifiName, NULL, NULL, Connected, NULL, CapturedAtUtc, FirmwareVersion
    FROM DeviceHealthSnapshots
    WHERE DeviceId IS NOT NULL;
");

            _ = migrationBuilder.DropTable(
                name: "DeviceHealthSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "DeviceHealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Connected = table.Column<bool>(type: "INTEGER", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DownloadEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Rssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_DeviceHealthSnapshots", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DeviceId_CapturedAtUtc",
                table: "DeviceHealthSnapshots",
                columns: new[] { "DeviceId", "CapturedAtUtc" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthSnapshots_DownloadEventId",
                table: "DeviceHealthSnapshots",
                column: "DownloadEventId");
        }
    }
}
