using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoForensics.Data.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceSettingsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatteryLife2",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatteryVoltage",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatteryVoltage2",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChimeVolume",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DingAudioId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DingAudioUserId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoNotDisturbSecondsLeft",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoorbellVolume",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAudioRecording",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableVod",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableWhiteLeds",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExposureControl",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LedStatus",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LightBrightness",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LightSettingsMetadataJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveViewPresetProfile",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotionAudioId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotionAudioUserId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MotionDetectionEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotionSnoozeJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotionSnoozePresetProfile",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NightMode",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NightModeOn",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Devices",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerFirstName",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerLastName",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSensitivity1",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity1",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity2",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity3",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSettingsZoneEnable",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PirSettingsZoneMask",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RingRingId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RingType",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettingsChimeDuration",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SettingsChimeEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettingsChimeType",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettingsMetadataJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SirenSecondsRemaining",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VodStatus",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VodSuspended",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RingHealthId",
                table: "DeviceHealths",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RingtonesEnabled",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowVodSettings",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlertValue",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceAdvancedMotionZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAdvancedMotionZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceAdvancedObjectSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SettingKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Day = table.Column<double>(type: "REAL", nullable: true),
                    Night = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAdvancedObjectSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceHealthRpcCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceHealthId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Command = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceHealthRpcCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceHealthRpcCommands_DeviceHealths_DeviceHealthId",
                        column: x => x.DeviceHealthId,
                        principalTable: "DeviceHealths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLiveViewPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Preset = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLiveViewPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceMotionSnoozePresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Preset = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceMotionSnoozePresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: true),
                    Style = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceAdvancedMotionZoneVertices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceAdvancedMotionZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: true),
                    Y = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceAdvancedMotionZoneVertices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceAdvancedMotionZoneVertices_DeviceAdvancedMotionZones_DeviceAdvancedMotionZoneId",
                        column: x => x.DeviceAdvancedMotionZoneId,
                        principalTable: "DeviceAdvancedMotionZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceZoneDetectionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DetectionType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceZoneDetectionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceZoneDetectionTypes_DeviceZones_DeviceZoneId",
                        column: x => x.DeviceZoneId,
                        principalTable: "DeviceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceZoneVertices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: true),
                    Y = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceZoneVertices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceZoneVertices_DeviceZones_DeviceZoneId",
                        column: x => x.DeviceZoneId,
                        principalTable: "DeviceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedMotionZones_DeviceId",
                table: "DeviceAdvancedMotionZones",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedMotionZoneVertices_DeviceAdvancedMotionZoneId",
                table: "DeviceAdvancedMotionZoneVertices",
                column: "DeviceAdvancedMotionZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedObjectSettings_DeviceId",
                table: "DeviceAdvancedObjectSettings",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRpcCommands_DeviceHealthId",
                table: "DeviceHealthRpcCommands",
                column: "DeviceHealthId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLiveViewPresets_DeviceId",
                table: "DeviceLiveViewPresets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMotionSnoozePresets_DeviceId",
                table: "DeviceMotionSnoozePresets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceZoneDetectionTypes_DeviceZoneId",
                table: "DeviceZoneDetectionTypes",
                column: "DeviceZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceZones_DeviceId",
                table: "DeviceZones",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceZoneVertices_DeviceZoneId",
                table: "DeviceZoneVertices",
                column: "DeviceZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceAdvancedMotionZoneVertices");

            migrationBuilder.DropTable(
                name: "DeviceAdvancedObjectSettings");

            migrationBuilder.DropTable(
                name: "DeviceHealthRpcCommands");

            migrationBuilder.DropTable(
                name: "DeviceLiveViewPresets");

            migrationBuilder.DropTable(
                name: "DeviceMotionSnoozePresets");

            migrationBuilder.DropTable(
                name: "DeviceZoneDetectionTypes");

            migrationBuilder.DropTable(
                name: "DeviceZoneVertices");

            migrationBuilder.DropTable(
                name: "DeviceAdvancedMotionZones");

            migrationBuilder.DropTable(
                name: "DeviceZones");

            migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionEnabled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryLife2",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryVoltage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryVoltage2",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ChimeVolume",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DingAudioId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DingAudioUserId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DoNotDisturbSecondsLeft",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DoorbellVolume",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "EnableAudioRecording",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "EnableVod",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "EnableWhiteLeds",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ExposureControl",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LedStatus",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LightBrightness",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LightSettingsMetadataJson",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LiveViewPresetProfile",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MotionAudioId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MotionAudioUserId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MotionDetectionEnabled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MotionSnoozeJson",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MotionSnoozePresetProfile",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NightMode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NightModeOn",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OwnerFirstName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OwnerLastName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSensitivity1",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity1",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity2",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity3",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSettingsZoneEnable",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PirSettingsZoneMask",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RingRingId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RingType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SettingsChimeDuration",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SettingsChimeEnabled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SettingsChimeType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SettingsMetadataJson",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SirenSecondsRemaining",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "VodStatus",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "VodSuspended",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RingHealthId",
                table: "DeviceHealths");

            migrationBuilder.DropColumn(
                name: "RingtonesEnabled",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "ShowVodSettings",
                table: "DeviceFeatures");

            migrationBuilder.DropColumn(
                name: "AlertValue",
                table: "DeviceAlerts");
        }
    }
}
