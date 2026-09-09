using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Add_DeviceMetadataNormalization_HealthFeaturesLocationAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceFeaturesId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceHealthId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceLocationId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MotionsEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShowRecordings = table.Column<bool>(type: "INTEGER", nullable: true),
                    AdvancedMotionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    PeopleOnlyEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    ShadowCorrectionEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    MotionMessageEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    NightVisionEnabled = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatteryPercentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    BatteryVoltageValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    WifiSignalRssi = table.Column<int>(type: "INTEGER", nullable: true),
                    WifiName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsExternalPowerConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    OtaStatus = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAlerts_DeviceId_AlertType",
                table: "DeviceAlerts",
                columns: new[] { "DeviceId", "AlertType" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFeatures_DeviceId",
                table: "DeviceFeatures",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealths_DeviceId_CapturedAtUtc",
                table: "DeviceHealths",
                columns: new[] { "DeviceId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocations_DeviceId",
                table: "DeviceLocations",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceAlerts");

            migrationBuilder.DropTable(
                name: "DeviceFeatures");

            migrationBuilder.DropTable(
                name: "DeviceHealths");

            migrationBuilder.DropTable(
                name: "DeviceLocations");

            migrationBuilder.DropColumn(
                name: "DeviceFeaturesId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceHealthId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceLocationId",
                table: "Devices");
        }
    }
}
