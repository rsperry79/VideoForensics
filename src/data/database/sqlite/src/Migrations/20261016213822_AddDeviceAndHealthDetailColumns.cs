using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAndHealthDetailColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Devices",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "BatteryLife",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "ExternalConnection",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "FirmwareVersion",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Devices",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Devices",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Owned",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RingDeviceId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RingKind",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Stolen",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "Subscribed",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "SubscribedMotions",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "AverageSignalCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "AverageSignalStrength",
                table: "DeviceHealths",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "BatteryPercentageCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "BatteryPresent",
                table: "DeviceHealths",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "BatteryVoltageCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "Firmware",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "FirmwareVersionStatus",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LatestSignalCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "LatestSignalStrength",
                table: "DeviceHealths",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "NetworkConnectionValue",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "PacketLoss",
                table: "DeviceHealths",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "PacketLossCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<double>(
                name: "PacketLossStrength",
                table: "DeviceHealths",
                type: "REAL",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "PtzConnected",
                table: "DeviceHealths",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RssiCategory",
                table: "DeviceHealths",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "DeviceHealths",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "WifiIsRingNetwork",
                table: "DeviceHealths",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropColumn(
                name: "Address",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "BatteryLife",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "ExternalConnection",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "FirmwareVersion",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "Owned",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "RingDeviceId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "RingKind",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "Stolen",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "Subscribed",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SubscribedMotions",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "AverageSignalCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "AverageSignalStrength",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "BatteryPercentageCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "BatteryPresent",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "BatteryVoltageCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "Firmware",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "FirmwareVersionStatus",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "LatestSignalCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "LatestSignalStrength",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "NetworkConnectionValue",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "PacketLoss",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "PacketLossCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "PacketLossStrength",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "PtzConnected",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "RssiCategory",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "WifiIsRingNetwork",
                table: "DeviceHealths");
        }
    }
}
