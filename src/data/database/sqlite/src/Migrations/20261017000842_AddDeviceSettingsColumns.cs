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
            _ = migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "BatteryLife2",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<decimal>(
                name: "BatteryVoltage",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<decimal>(
                name: "BatteryVoltage2",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "ChimeVolume",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "DingAudioId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "DingAudioUserId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "DoNotDisturbSecondsLeft",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "DoorbellVolume",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "EnableAudioRecording",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "EnableVod",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "EnableWhiteLeds",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "ExposureControl",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LedStatus",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "LightBrightness",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LightSettingsMetadataJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "LiveViewPresetProfile",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MotionAudioId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MotionAudioUserId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "MotionDetectionEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MotionSnoozeJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "MotionSnoozePresetProfile",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "NightMode",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "NightModeOn",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Devices",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "OwnerFirstName",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "OwnerLastName",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSensitivity1",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity1",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity2",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSettingsSensitivity3",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSettingsZoneEnable",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "PirSettingsZoneMask",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RingRingId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "RingType",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SettingsChimeDuration",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "SettingsChimeEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SettingsChimeType",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "SettingsMetadataJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.AddColumn<int>(
                name: "SirenSecondsRemaining",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "VodStatus",
                table: "Devices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "VodSuspended",
                table: "Devices",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<long>(
                name: "RingHealthId",
                table: "DeviceHealths",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "RingtonesEnabled",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<bool>(
                name: "ShowVodSettings",
                table: "DeviceFeatures",
                type: "INTEGER",
                nullable: true);

            _ = migrationBuilder.AddColumn<string>(
                name: "AlertValue",
                table: "DeviceAlerts",
                type: "TEXT",
                nullable: true);

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceAdvancedMotionZones", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceAdvancedObjectSettings", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceHealthRpcCommands", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceHealthRpcCommands_DeviceHealths_DeviceHealthId",
                        column: x => x.DeviceHealthId,
                        principalTable: "DeviceHealths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceLiveViewPresets", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceMotionSnoozePresets", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceZones", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceAdvancedMotionZoneVertices", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceAdvancedMotionZoneVertices_DeviceAdvancedMotionZones_DeviceAdvancedMotionZoneId",
                        column: x => x.DeviceAdvancedMotionZoneId,
                        principalTable: "DeviceAdvancedMotionZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceZoneDetectionTypes", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceZoneDetectionTypes_DeviceZones_DeviceZoneId",
                        column: x => x.DeviceZoneId,
                        principalTable: "DeviceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
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
                    _ = table.PrimaryKey("PK_DeviceZoneVertices", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_DeviceZoneVertices_DeviceZones_DeviceZoneId",
                        column: x => x.DeviceZoneId,
                        principalTable: "DeviceZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedMotionZones_DeviceId",
                table: "DeviceAdvancedMotionZones",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedMotionZoneVertices_DeviceAdvancedMotionZoneId",
                table: "DeviceAdvancedMotionZoneVertices",
                column: "DeviceAdvancedMotionZoneId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceAdvancedObjectSettings_DeviceId",
                table: "DeviceAdvancedObjectSettings",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceHealthRpcCommands_DeviceHealthId",
                table: "DeviceHealthRpcCommands",
                column: "DeviceHealthId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceLiveViewPresets_DeviceId",
                table: "DeviceLiveViewPresets",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceMotionSnoozePresets_DeviceId",
                table: "DeviceMotionSnoozePresets",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceZoneDetectionTypes_DeviceZoneId",
                table: "DeviceZoneDetectionTypes",
                column: "DeviceZoneId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceZones_DeviceId",
                table: "DeviceZones",
                column: "DeviceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_DeviceZoneVertices_DeviceZoneId",
                table: "DeviceZoneVertices",
                column: "DeviceZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "DeviceAdvancedMotionZoneVertices");

            _ = migrationBuilder.DropTable(
                name: "DeviceAdvancedObjectSettings");

            _ = migrationBuilder.DropTable(
                name: "DeviceHealthRpcCommands");

            _ = migrationBuilder.DropTable(
                name: "DeviceLiveViewPresets");

            _ = migrationBuilder.DropTable(
                name: "DeviceMotionSnoozePresets");

            _ = migrationBuilder.DropTable(
                name: "DeviceZoneDetectionTypes");

            _ = migrationBuilder.DropTable(
                name: "DeviceZoneVertices");

            _ = migrationBuilder.DropTable(
                name: "DeviceAdvancedMotionZones");

            _ = migrationBuilder.DropTable(
                name: "DeviceZones");

            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionEnabled",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "AdvancedMotionDetectionHumanOnlyMode",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "BatteryLife2",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "BatteryVoltage",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "BatteryVoltage2",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "ChimeVolume",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "DingAudioId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "DingAudioUserId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "DoNotDisturbSecondsLeft",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "DoorbellVolume",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "EnableAudioRecording",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "EnableVod",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "EnableWhiteLeds",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "ExposureControl",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LedStatus",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LightBrightness",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LightSettingsMetadataJson",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "LiveViewPresetProfile",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MotionAudioId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MotionAudioUserId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MotionDetectionEnabled",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MotionSnoozeJson",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "MotionSnoozePresetProfile",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "NightMode",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "NightModeOn",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "OwnerFirstName",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "OwnerLastName",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSensitivity1",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity1",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity2",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSettingsSensitivity3",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSettingsZoneEnable",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "PirSettingsZoneMask",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "RingRingId",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "RingType",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SettingsChimeDuration",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SettingsChimeEnabled",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SettingsChimeType",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SettingsMetadataJson",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "SirenSecondsRemaining",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "VodStatus",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "VodSuspended",
                table: "Devices");

            _ = migrationBuilder.DropColumn(
                name: "RingHealthId",
                table: "DeviceHealths");

            _ = migrationBuilder.DropColumn(
                name: "RingtonesEnabled",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "ShowVodSettings",
                table: "DeviceFeatures");

            _ = migrationBuilder.DropColumn(
                name: "AlertValue",
                table: "DeviceAlerts");
        }
    }
}
