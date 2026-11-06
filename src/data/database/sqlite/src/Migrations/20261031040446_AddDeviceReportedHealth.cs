using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceReportedHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAtUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LedSecondsRemaining",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedAtUtc",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceReportedBatteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    BatteryNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    BatteryId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Percentage = table.Column<int>(type: "INTEGER", nullable: true),
                    Present = table.Column<bool>(type: "INTEGER", nullable: true),
                    Charging = table.Column<bool>(type: "INTEGER", nullable: true),
                    Voltage = table.Column<decimal>(type: "TEXT", nullable: true),
                    PercentageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    VoltageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Temp = table.Column<double>(type: "REAL", nullable: true),
                    AuthStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Family = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FwVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OtaStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceReportedBatteries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceReportedHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Connected = table.Column<bool>(type: "INTEGER", nullable: true),
                    FloodlightOn = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastUpdateTime = table.Column<long>(type: "INTEGER", nullable: true),
                    Rssi = table.Column<double>(type: "REAL", nullable: true),
                    WhiteLedOn = table.Column<bool>(type: "INTEGER", nullable: true),
                    BatteryTemp = table.Column<double>(type: "REAL", nullable: true),
                    Bandwidth = table.Column<long>(type: "INTEGER", nullable: true),
                    SirenOn = table.Column<bool>(type: "INTEGER", nullable: true),
                    StatusTime = table.Column<long>(type: "INTEGER", nullable: true),
                    BatteryError = table.Column<bool>(type: "INTEGER", nullable: true),
                    FirmwareAvgBitrate = table.Column<long>(type: "INTEGER", nullable: true),
                    StreamResolution = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoPacketsTotal = table.Column<long>(type: "INTEGER", nullable: true),
                    UptimeSec = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrentBandwidth = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrentBandwidthMb = table.Column<decimal>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceReportedHealths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SchemaId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsSidewalkGateway = table.Column<bool>(type: "INTEGER", nullable: true),
                    DeviceResourceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RingNetId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ActiveScheduleUuid = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FlickElimRecommendedMode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExtPowerState = table.Column<int>(type: "INTEGER", nullable: true),
                    EncryptionGroupId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AccountGroupRevisionId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RingDeviceHealthExtras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FirmwareVersionStatus = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExternalConnection = table.Column<bool>(type: "INTEGER", nullable: true),
                    NetworkConnectionValue = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    NightModeOn = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtaStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RssConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    RssiCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    BatteryPercentageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    BatteryVoltageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    BatteryPresent = table.Column<bool>(type: "INTEGER", nullable: true),
                    BatteryPercentage = table.Column<int>(type: "INTEGER", nullable: true),
                    BatteryVoltage = table.Column<decimal>(type: "TEXT", nullable: true),
                    SecondBatteryPercentageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SecondBatteryVoltageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SidewalkConnection = table.Column<bool>(type: "INTEGER", nullable: true),
                    TransformerVoltage = table.Column<decimal>(type: "TEXT", nullable: true),
                    TransformerVoltageCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TransformerScore = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PacketLoss = table.Column<double>(type: "REAL", nullable: true),
                    PacketLossCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    HatchOpen = table.Column<bool>(type: "INTEGER", nullable: true),
                    VodEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    BandwidthLastTime = table.Column<long>(type: "INTEGER", nullable: true),
                    WifiIsRingNetwork = table.Column<bool>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PackageWarningActive = table.Column<bool>(type: "INTEGER", nullable: true),
                    ExtPowerState = table.Column<int>(type: "INTEGER", nullable: true),
                    PtzConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RingDeviceHealthExtras", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceReportedBatteries_DeviceId_Position",
                table: "DeviceReportedBatteries",
                columns: new[] { "DeviceId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceReportedHealths_DeviceId",
                table: "DeviceReportedHealths",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceDetails_DeviceId",
                table: "RingDeviceDetails",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RingDeviceHealthExtras_DeviceId",
                table: "RingDeviceHealthExtras",
                column: "DeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceReportedBatteries");

            migrationBuilder.DropTable(
                name: "DeviceReportedHealths");

            migrationBuilder.DropTable(
                name: "RingDeviceDetails");

            migrationBuilder.DropTable(
                name: "RingDeviceHealthExtras");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LedSecondsRemaining",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SharedAtUtc",
                table: "Devices");
        }
    }
}
